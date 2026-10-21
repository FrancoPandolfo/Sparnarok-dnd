using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.Services.Combat;
using Sparnarok.Api.Domain.Services.Dice;
using Sparnarok.Api.Domain.Services.Rng;
using Sparnarok.Api.Infrastructure.Persistence;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Implementación del servicio de simulación de combate de D&amp;D 5e (2024).
/// Orquesta la resolución estocástica de iniciativa, ataques d20 contra CA,
/// cálculo de daño (dados + modificadores), impactos críticos y logs ronda a ronda.
/// </summary>
public class CombatSimulatorService : ICombatSimulatorService
{
    private readonly IRandomProvider _random;
    private readonly IDiceRoller _diceRoller;
    private readonly SparnarokDbContext? _context;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="CombatSimulatorService"/>.
    /// </summary>
    /// <param name="random">Proveedor de números aleatorios (producción o determinista para tests).</param>
    /// <param name="diceRoller">Servicio evaluador de fórmulas de dados.</param>
    /// <param name="context">Contexto de base de datos relacional (opcional para ejecución pura en memoria).</param>
    public CombatSimulatorService(
        IRandomProvider random,
        IDiceRoller diceRoller,
        SparnarokDbContext? context = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _diceRoller = diceRoller ?? throw new ArgumentNullException(nameof(diceRoller));
        _context = context;
    }

    /// <inheritdoc />
    public CombatSimulationResult SimulateDuel(
        Creature combatant1,
        Weapon? weapon1,
        Creature combatant2,
        Weapon? weapon2)
    {
        ArgumentNullException.ThrowIfNull(combatant1);
        ArgumentNullException.ThrowIfNull(combatant2);

        if (weapon1 == null && (!combatant1.IsPlayerCharacter || !combatant1.ExpectedDpr.HasValue || combatant1.ExpectedDpr <= 0))
            throw new ArgumentNullException(nameof(weapon1), $"El combatiente {combatant1.Name} requiere un arma equipada al no poseer ExpectedDpr.");

        if (weapon2 == null && (!combatant2.IsPlayerCharacter || !combatant2.ExpectedDpr.HasValue || combatant2.ExpectedDpr <= 0))
            throw new ArgumentNullException(nameof(weapon2), $"El combatiente {combatant2.Name} requiere un arma equipada al no poseer ExpectedDpr.");

        var simulationId = Guid.NewGuid();
        var logs = new List<CombatEventLog>();

        // 1. Tirada de Iniciativa: d20 + Modificador de Destreza
        var dexMod1 = combatant1.AbilityScores.GetModifier(AbilityScoreType.Dexterity);
        var dexMod2 = combatant2.AbilityScores.GetModifier(AbilityScoreType.Dexterity);

        var initRoll1 = _random.RollD20();
        var initRoll2 = _random.RollD20();

        var totalInit1 = initRoll1 + dexMod1;
        var totalInit2 = initRoll2 + dexMod2;

        // Desempate: mayor modificador de Destreza; si persiste, tirada aleatoria de moneda
        bool combatant1GoesFirst;
        if (totalInit1 > totalInit2)
        {
            combatant1GoesFirst = true;
        }
        else if (totalInit2 > totalInit1)
        {
            combatant1GoesFirst = false;
        }
        else
        {
            combatant1GoesFirst = dexMod1 != dexMod2 ? dexMod1 > dexMod2 : _random.Next(0, 2) == 0;
        }

        var fighterA = new Fighter(
            combatant1GoesFirst ? combatant1 : combatant2,
            combatant1GoesFirst ? weapon1 : weapon2,
            combatant1GoesFirst ? totalInit1 : totalInit2);

        var fighterB = new Fighter(
            combatant1GoesFirst ? combatant2 : combatant1,
            combatant1GoesFirst ? weapon2 : weapon1,
            combatant1GoesFirst ? totalInit2 : totalInit1);

        int currentRound = 0;
        const int maxRounds = 100; // Salvaguarda contra bucles infinitos

        // 2. Bucle de Combate Ronda a Ronda
        while (fighterA.CurrentHp > 0 && fighterB.CurrentHp > 0 && currentRound < maxRounds)
        {
            currentRound++;

            // Turno del Primer Atacante (Fighter A)
            ExecuteAttack(currentRound, fighterA, fighterB, logs);
            if (fighterB.CurrentHp <= 0) break;

            // Turno del Segundo Atacante (Fighter B)
            ExecuteAttack(currentRound, fighterB, fighterA, logs);
            if (fighterA.CurrentHp <= 0) break;
        }

        // 3. Determinación del Vencedor
        string winnerName;
        Guid? winnerId;

        if (fighterA.CurrentHp > 0 && fighterB.CurrentHp <= 0)
        {
            winnerName = fighterA.Creature.Name;
            winnerId = fighterA.Creature.Id;
        }
        else if (fighterB.CurrentHp > 0 && fighterA.CurrentHp <= 0)
        {
            winnerName = fighterB.Creature.Name;
            winnerId = fighterB.Creature.Id;
        }
        else
        {
            winnerName = "Empate";
            winnerId = null;
        }

        var weaponName1 = weapon1?.Name ?? (combatant1.ExpectedDpr.HasValue ? $"DPR Baseline ({combatant1.Archetype ?? "Hero"})" : "Unarmed");
        var weaponName2 = weapon2?.Name ?? (combatant2.ExpectedDpr.HasValue ? $"DPR Baseline ({combatant2.Archetype ?? "Hero"})" : "Unarmed");

        var summary1 = new CombatantSummary(
            combatant1.Id,
            combatant1.Name,
            weaponName1,
            totalInit1,
            combatant1.HitPoints.Max,
            combatant1.Id == fighterA.Creature.Id ? fighterA.CurrentHp : fighterB.CurrentHp,
            (combatant1.Id == fighterA.Creature.Id ? fighterA.CurrentHp : fighterB.CurrentHp) > 0);

        var summary2 = new CombatantSummary(
            combatant2.Id,
            combatant2.Name,
            weaponName2,
            totalInit2,
            combatant2.HitPoints.Max,
            combatant2.Id == fighterA.Creature.Id ? fighterA.CurrentHp : fighterB.CurrentHp,
            (combatant2.Id == fighterA.Creature.Id ? fighterA.CurrentHp : fighterB.CurrentHp) > 0);

        return new CombatSimulationResult(
            simulationId,
            summary1,
            summary2,
            winnerName,
            winnerId,
            currentRound,
            logs.AsReadOnly(),
            DateTime.UtcNow);
    }


    /// <inheritdoc />
    public FastDuelResult SimulateFastDuel(
        Creature combatant1,
        Weapon? weapon1,
        Creature combatant2,
        Weapon? weapon2)
    {
        ArgumentNullException.ThrowIfNull(combatant1);
        ArgumentNullException.ThrowIfNull(combatant2);

        if (weapon1 == null && (!combatant1.IsPlayerCharacter || !combatant1.ExpectedDpr.HasValue || combatant1.ExpectedDpr <= 0))
            throw new ArgumentNullException(nameof(weapon1), $"El combatiente {combatant1.Name} requiere un arma equipada al no poseer ExpectedDpr.");

        if (weapon2 == null && (!combatant2.IsPlayerCharacter || !combatant2.ExpectedDpr.HasValue || combatant2.ExpectedDpr <= 0))
            throw new ArgumentNullException(nameof(weapon2), $"El combatiente {combatant2.Name} requiere un arma equipada al no poseer ExpectedDpr.");

        var dexMod1 = combatant1.AbilityScores.GetModifier(AbilityScoreType.Dexterity);
        var dexMod2 = combatant2.AbilityScores.GetModifier(AbilityScoreType.Dexterity);

        var initRoll1 = _random.RollD20();
        var initRoll2 = _random.RollD20();

        var totalInit1 = initRoll1 + dexMod1;
        var totalInit2 = initRoll2 + dexMod2;

        bool combatant1GoesFirst;
        if (totalInit1 > totalInit2)
        {
            combatant1GoesFirst = true;
        }
        else if (totalInit2 > totalInit1)
        {
            combatant1GoesFirst = false;
        }
        else
        {
            combatant1GoesFirst = dexMod1 != dexMod2 ? dexMod1 > dexMod2 : _random.Next(0, 2) == 0;
        }

        var fighterA = new Fighter(
            combatant1GoesFirst ? combatant1 : combatant2,
            combatant1GoesFirst ? weapon1 : weapon2,
            combatant1GoesFirst ? totalInit1 : totalInit2);

        var fighterB = new Fighter(
            combatant1GoesFirst ? combatant2 : combatant1,
            combatant1GoesFirst ? weapon2 : weapon1,
            combatant1GoesFirst ? totalInit2 : totalInit1);

        int currentRound = 0;
        const int maxRounds = 100;

        int damageDealtBy1 = 0;
        int damageDealtBy2 = 0;

        while (fighterA.CurrentHp > 0 && fighterB.CurrentHp > 0 && currentRound < maxRounds)
        {
            currentRound++;

            var dmgA = ExecuteFastAttack(fighterA, fighterB);
            if (fighterA.Creature.Id == combatant1.Id) damageDealtBy1 += dmgA;
            else damageDealtBy2 += dmgA;

            if (fighterB.CurrentHp <= 0) break;

            var dmgB = ExecuteFastAttack(fighterB, fighterA);
            if (fighterB.Creature.Id == combatant1.Id) damageDealtBy1 += dmgB;
            else damageDealtBy2 += dmgB;

            if (fighterA.CurrentHp <= 0) break;
        }

        Guid? winnerId = null;
        if (fighterA.CurrentHp > 0 && fighterB.CurrentHp <= 0)
        {
            winnerId = fighterA.Creature.Id;
        }
        else if (fighterB.CurrentHp > 0 && fighterA.CurrentHp <= 0)
        {
            winnerId = fighterB.Creature.Id;
        }

        return new FastDuelResult(winnerId, currentRound, damageDealtBy1, damageDealtBy2);
    }

    private int ExecuteFastAttack(Fighter attacker, Fighter defender)
    {
        // Flujo alternativo de ExpectedDpr para Personajes de Jugador:
        // Se omite la tirada d20 contra CA y se aplica daño directo con ±15% de varianza
        if (attacker.Creature.IsPlayerCharacter && attacker.Creature.ExpectedDpr.HasValue && attacker.Creature.ExpectedDpr.Value > 0)
        {
            var baseDpr = attacker.Creature.ExpectedDpr.Value;
            var variancePercent = _random.Next(-15, 16);
            var factor = 1.0 + (variancePercent / 100.0);
            var damage = Math.Max(1, (int)Math.Round(baseDpr * factor, MidpointRounding.AwayFromZero));
            defender.CurrentHp = Math.Max(0, defender.CurrentHp - damage);
            return damage;
        }

        if (attacker.Weapon == null)
            return 0;

        var abilityMod = GetWeaponAbilityModifier(attacker.Creature, attacker.Weapon);
        var attackBonus = abilityMod + attacker.Creature.ProficiencyBonus;

        var d20 = _random.RollD20();
        bool isNatural20 = d20 == 20;
        bool isNatural1 = d20 == 1;

        if (isNatural1) return 0;

        if (isNatural20 || (d20 + attackBonus) >= defender.EffectiveAc)
        {
            var damageResult = _diceRoller.Roll(
                attacker.Weapon.Damage,
                additionalModifier: abilityMod,
                isCritical: isNatural20);

            var damage = damageResult.Total;
            defender.CurrentHp = Math.Max(0, defender.CurrentHp - damage);
            return damage;
        }

        return 0;
    }

    /// <inheritdoc />
    public async Task<CombatSimulationResult> SimulateDuelAsync(
        Guid creature1Id,
        Guid? weapon1Id,
        Guid creature2Id,
        Guid? weapon2Id,
        CancellationToken cancellationToken = default)
    {
        if (_context == null)
            throw new InvalidOperationException("El contexto de base de datos no fue proporcionado para operaciones asíncronas de persistencia.");

        var creature1 = await _context.Creatures
            .Include(c => c.EquippedArmor)
            .Include(c => c.EquippedShield)
            .FirstOrDefaultAsync(c => c.Id == creature1Id, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró la criatura con ID: {creature1Id}");

        Weapon? weapon1 = null;
        if (weapon1Id.HasValue && weapon1Id.Value != Guid.Empty)
        {
            weapon1 = await _context.Weapons
                .FirstOrDefaultAsync(w => w.Id == weapon1Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"No se encontró el arma con ID: {weapon1Id}");
        }
        else if (!creature1.IsPlayerCharacter || !creature1.ExpectedDpr.HasValue || creature1.ExpectedDpr.Value <= 0)
        {
            throw new KeyNotFoundException($"Se requiere un arma para la criatura {creature1.Name} al no tener configurado ExpectedDpr.");
        }

        var creature2 = await _context.Creatures
            .Include(c => c.EquippedArmor)
            .Include(c => c.EquippedShield)
            .FirstOrDefaultAsync(c => c.Id == creature2Id, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró la criatura con ID: {creature2Id}");

        Weapon? weapon2 = null;
        if (weapon2Id.HasValue && weapon2Id.Value != Guid.Empty)
        {
            weapon2 = await _context.Weapons
                .FirstOrDefaultAsync(w => w.Id == weapon2Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"No se encontró el arma con ID: {weapon2Id}");
        }
        else if (!creature2.IsPlayerCharacter || !creature2.ExpectedDpr.HasValue || creature2.ExpectedDpr.Value <= 0)
        {
            throw new KeyNotFoundException($"Se requiere un arma para la criatura {creature2.Name} al no tener configurado ExpectedDpr.");
        }

        return SimulateDuel(creature1, weapon1, creature2, weapon2);
    }

    /// <summary>
    /// Resuelve un ataque individual de un combatiente contra otro siguiendo las reglas canónicas de D&amp;D 5e (2024).
    /// </summary>
    private void ExecuteAttack(
        int round,
        Fighter attacker,
        Fighter defender,
        List<CombatEventLog> logs)
    {
        // Si el atacante es un Player Character con ExpectedDpr configurado,
        // se omite la tirada de ataque convencional d20 vs CA y se reduce directamente la vida del defensor con ±15% de varianza
        if (attacker.Creature.IsPlayerCharacter && attacker.Creature.ExpectedDpr.HasValue && attacker.Creature.ExpectedDpr.Value > 0)
        {
            var baseDpr = attacker.Creature.ExpectedDpr.Value;
            var variancePercent = _random.Next(-15, 16); // -15% a +15%
            var factor = 1.0 + (variancePercent / 100.0);
            var dprDamage = Math.Max(1, (int)Math.Round(baseDpr * factor, MidpointRounding.AwayFromZero));

            defender.CurrentHp = Math.Max(0, defender.CurrentHp - dprDamage);

            var weaponName = attacker.Weapon?.Name ?? $"DPR Baseline ({attacker.Creature.Archetype ?? "Hero"})";
            var sign = variancePercent >= 0 ? "+" : "";
            var dprDescription = $"{attacker.Creature.Name} ejecuta su turno con DPR de arquetipo ({baseDpr:F1} DPR, varianza {sign}{variancePercent}%): inflige {dprDamage} de daño directo a {defender.Creature.Name}. (HP restantes: {defender.CurrentHp})";

            logs.Add(new CombatEventLog(
                Round: round,
                AttackerName: attacker.Creature.Name,
                DefenderName: defender.Creature.Name,
                WeaponName: weaponName,
                D20Roll: 0,
                AttackModifier: 0,
                TotalAttackRoll: 0,
                TargetArmorClass: defender.EffectiveAc,
                IsNatural20: false,
                IsNatural1: false,
                IsHit: true,
                DamageDealt: dprDamage,
                DefenderRemainingHp: defender.CurrentHp,
                Description: dprDescription));

            return;
        }


        if (attacker.Weapon == null)
            throw new InvalidOperationException($"El combatiente {attacker.Creature.Name} no tiene un arma equipada ni ExpectedDpr configurado.");

        // Determinación de la característica gobernante del ataque según propiedades del arma
        var abilityMod = GetWeaponAbilityModifier(attacker.Creature, attacker.Weapon);
        var attackBonus = abilityMod + attacker.Creature.ProficiencyBonus;

        var d20 = _random.RollD20();
        bool isNatural20 = d20 == 20;
        bool isNatural1 = d20 == 1;

        int totalAttack = d20 + attackBonus;
        int targetAc = defender.EffectiveAc;

        // Regla 5e: 20 natural impacta críticamente sin importar la CA. 1 natural falla automáticamente.
        bool isHit;
        if (isNatural20)
        {
            isHit = true;
        }
        else if (isNatural1)
        {
            isHit = false;
        }
        else
        {
            isHit = totalAttack >= targetAc;
        }

        int damageDealt = 0;
        string description;

        if (isHit)
        {
            // Tirada de daño (dados duplicados si es crítico + modificador)
            var damageResult = _diceRoller.Roll(
                attacker.Weapon.Damage,
                additionalModifier: abilityMod,
                isCritical: isNatural20);

            damageDealt = damageResult.Total;
            defender.CurrentHp = Math.Max(0, defender.CurrentHp - damageDealt);

            if (isNatural20)
            {
                description = $"{attacker.Creature.Name} obtiene un ¡20 NATURAL! Impacto crítico con {attacker.Weapon.Name} infligiendo {damageDealt} de daño a {defender.Creature.Name}. (HP restantes: {defender.CurrentHp})";
            }
            else
            {
                description = $"{attacker.Creature.Name} ataca con {attacker.Weapon.Name} ({d20} + {attackBonus} = {totalAttack} vs CA {targetAc}): IMPACTO. Inflige {damageDealt} de daño. (HP restantes: {defender.CurrentHp})";
            }
        }
        else
        {
            if (isNatural1)
            {
                description = $"{attacker.Creature.Name} obtiene un 1 NATURAL con {attacker.Weapon.Name}: FALLO AUTOMÁTICO.";
            }
            else
            {
                description = $"{attacker.Creature.Name} ataca con {attacker.Weapon.Name} ({d20} + {attackBonus} = {totalAttack} vs CA {targetAc}): FALLA.";
            }
        }

        logs.Add(new CombatEventLog(
            Round: round,
            AttackerName: attacker.Creature.Name,
            DefenderName: defender.Creature.Name,
            WeaponName: attacker.Weapon.Name,
            D20Roll: d20,
            AttackModifier: attackBonus,
            TotalAttackRoll: totalAttack,
            TargetArmorClass: targetAc,
            IsNatural20: isNatural20,
            IsNatural1: isNatural1,
            IsHit: isHit,
            DamageDealt: damageDealt,
            DefenderRemainingHp: defender.CurrentHp,
            Description: description));
    }

    /// <summary>
    /// Selecciona el modificador de característica gobernante de acuerdo con las propiedades de D&amp;D 5e:
    /// - Finesse: Mayor entre Fuerza y Destreza.
    /// - Ranged (Ammunition sin Thrown): Destreza.
    /// - Melee estándar: Fuerza.
    /// </summary>
    private static int GetWeaponAbilityModifier(Creature creature, Weapon? weapon)
    {
        var strMod = creature.AbilityScores.GetModifier(AbilityScoreType.Strength);
        var dexMod = creature.AbilityScores.GetModifier(AbilityScoreType.Dexterity);

        if (weapon == null) return strMod;

        var props = weapon.Properties ?? string.Empty;

        if (props.Contains("Finesse", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(strMod, dexMod);
        }

        if (props.Contains("Ammunition", StringComparison.OrdinalIgnoreCase) &&
            !props.Contains("Thrown", StringComparison.OrdinalIgnoreCase))
        {
            return dexMod;
        }

        return strMod;
    }

    /// <summary>
    /// Estado mutable interno para el seguimiento del combatiente durante el bucle de simulación.
    /// </summary>
    private class Fighter
    {
        public Creature Creature { get; }
        public Weapon? Weapon { get; }
        public int Initiative { get; }
        public int CurrentHp { get; set; }
        public int EffectiveAc { get; }

        public Fighter(Creature creature, Weapon? weapon, int initiative)
        {
            Creature = creature;
            Weapon = weapon;
            Initiative = initiative;
            CurrentHp = creature.HitPoints.Current;
            EffectiveAc = creature.EffectiveArmorClass;
        }
    }
}

