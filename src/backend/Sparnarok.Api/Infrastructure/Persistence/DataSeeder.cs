using Microsoft.EntityFrameworkCore;
using Sparnarok.Api.Domain.Entities;
using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.ValueObjects;
using Action = Sparnarok.Api.Domain.Entities.Action;

namespace Sparnarok.Api.Infrastructure.Persistence;

/// <summary>
/// Provee inicialización y siembra de catálogo inicial de criaturas canónicas de D&amp;D 5e (SRD 2024).
/// </summary>
public static class DataSeeder
{
    /// <summary>
    /// Siembra un conjunto balanceado y rico de criaturas de demostración si la tabla está vacía.
    /// </summary>
    public static void SeedInitialData(SparnarokDbContext db)
    {
        if (db.Creatures.Any())
        {
            return;
        }

        var toppleMastery = db.WeaponMasteries.FirstOrDefault(m => m.Property == MasteryProperty.Topple);
        var sapMastery = db.WeaponMasteries.FirstOrDefault(m => m.Property == MasteryProperty.Sap);
        var vexMastery = db.WeaponMasteries.FirstOrDefault(m => m.Property == MasteryProperty.Vex);
        var pushMastery = db.WeaponMasteries.FirstOrDefault(m => m.Property == MasteryProperty.Push);

        var creatures = new List<Creature>();

        // 1. Goblin Scout (Small, Humanoid)
        var goblin = new Creature(
            "Goblin Scout",
            CreatureSize.Small,
            CreatureType.Humanoid,
            "Neutral Evil",
            0.25,
            30,
            new AbilityScores(8, 14, 10, 10, 8, 8),
            new HitPoints(7, 7),
            new ArmorClass(15, 0, false));
        goblin.AddAction(new Action(goblin.Id, "Scimitar Slash", ActionType.Action, "Ataque rápido y cortante de corta distancia.", true, 5, "1d6+2", DamageType.Slashing, AbilityScoreType.Dexterity));
        creatures.Add(goblin);

        // 2. Skeleton (Medium, Undead)
        var skeleton = new Creature(
            "Skeleton",
            CreatureSize.Medium,
            CreatureType.Undead,
            "Lawful Evil",
            0.25,
            30,
            new AbilityScores(10, 14, 15, 6, 8, 5),
            new HitPoints(13, 13),
            new ArmorClass(13, 0, false));
        skeleton.AddAction(new Action(skeleton.Id, "Shortsword Strike", ActionType.Action, "Estocada ósea con espada herrumbrosa.", true, 5, "1d6+2", DamageType.Piercing, AbilityScoreType.Dexterity));
        creatures.Add(skeleton);

        // 3. Dire Wolf (Large, Beast)
        var direWolf = new Creature(
            "Dire Wolf",
            CreatureSize.Large,
            CreatureType.Beast,
            "Unaligned",
            1.0,
            50,
            new AbilityScores(17, 15, 15, 3, 12, 7),
            new HitPoints(37, 37),
            new ArmorClass(14, 0, false));
        direWolf.AddAction(new Action(direWolf.Id, "Bite", ActionType.Action, "Mordisco feroz con probabilidad de derribar al objetivo.", true, 5, "2d6+3", DamageType.Piercing, AbilityScoreType.Strength));
        creatures.Add(direWolf);

        // 4. Bugbear Stalker (Medium, Humanoid)
        var bugbear = new Creature(
            "Bugbear Stalker",
            CreatureSize.Medium,
            CreatureType.Humanoid,
            "Chaotic Evil",
            1.0,
            30,
            new AbilityScores(15, 14, 13, 8, 11, 9),
            new HitPoints(27, 27),
            new ArmorClass(16, 2, false));
        bugbear.AddAction(new Action(bugbear.Id, "Morningstar Strike", ActionType.Action, "Golpe contundente con estrella matutina.", true, 5, "2d8+2", DamageType.Piercing, AbilityScoreType.Strength, sapMastery));
        if (sapMastery != null) bugbear.LearnWeaponMastery(sapMastery);
        creatures.Add(bugbear);

        // 5. Drow Duelist (Medium, Humanoid)
        var drow = new Creature(
            "Drow Duelist",
            CreatureSize.Medium,
            CreatureType.Humanoid,
            "Neutral Evil",
            2.0,
            30,
            new AbilityScores(10, 16, 12, 11, 13, 12),
            new HitPoints(33, 33),
            new ArmorClass(15, 0, true));
        drow.AddAction(new Action(drow.Id, "Rapier Thrust", ActionType.Action, "Estocada rápida de precisión quirúrgica.", true, 5, "1d8+3", DamageType.Piercing, AbilityScoreType.Dexterity, vexMastery));
        if (vexMastery != null) drow.LearnWeaponMastery(vexMastery);
        creatures.Add(drow);

        // 6. Gorgon Vanguard (Large, Monstrosity)
        var gorgon = new Creature(
            "Gorgon Vanguard",
            CreatureSize.Large,
            CreatureType.Monstrosity,
            "Neutral Evil",
            5.0,
            30,
            new AbilityScores(18, 14, 16, 10, 12, 8),
            new HitPoints(45, 45, 5),
            new ArmorClass(18, 2, false));
        gorgon.AddAction(new Action(gorgon.Id, "Iron Gore Strike", ActionType.Action, "Embiste con cuernos metálicos devastadores.", true, 5, "2d8+4", DamageType.Piercing, AbilityScoreType.Strength, toppleMastery));
        if (toppleMastery != null) gorgon.LearnWeaponMastery(toppleMastery);
        creatures.Add(gorgon);

        // 7. Red Dragon Wyrmling (Medium, Dragon)
        var redDragon = new Creature(
            "Red Dragon Wyrmling",
            CreatureSize.Medium,
            CreatureType.Dragon,
            "Chaotic Evil",
            4.0,
            30,
            new AbilityScores(19, 10, 17, 12, 11, 15),
            new HitPoints(75, 75),
            new ArmorClass(17, 0, false));
        redDragon.AddAction(new Action(redDragon.Id, "Bite", ActionType.Action, "Dentellada de llamas y colmillos dracónicos.", true, 5, "1d10+4", DamageType.Piercing, AbilityScoreType.Strength));
        creatures.Add(redDragon);

        // 8. Ogre Brute (Large, Giant)
        var ogre = new Creature(
            "Ogre Brute",
            CreatureSize.Large,
            CreatureType.Giant,
            "Chaotic Evil",
            2.0,
            40,
            new AbilityScores(19, 8, 16, 5, 7, 7),
            new HitPoints(59, 59),
            new ArmorClass(11, 0, false));
        ogre.AddAction(new Action(ogre.Id, "Greatclub Smash", ActionType.Action, "Aplastamiento brutal con un mazo de madera maciza.", true, 5, "2d8+4", DamageType.Bludgeoning, AbilityScoreType.Strength, pushMastery));
        if (pushMastery != null) ogre.LearnWeaponMastery(pushMastery);
        creatures.Add(ogre);

        // 9. Fire Elemental (Large, Elemental)
        var fireElemental = new Creature(
            "Fire Elemental",
            CreatureSize.Large,
            CreatureType.Elemental,
            "Neutral",
            5.0,
            50,
            new AbilityScores(10, 17, 16, 6, 10, 7),
            new HitPoints(102, 102),
            new ArmorClass(13, 0, false));
        fireElemental.AddAction(new Action(fireElemental.Id, "Touch", ActionType.Action, "Toque ígneo abrasador que enciende a los objetivos.", true, 5, "2d6+3", DamageType.Fire, AbilityScoreType.Dexterity));
        creatures.Add(fireElemental);

        // 10. Gelatinous Cube (Large, Ooze)
        var cube = new Creature(
            "Gelatinous Cube",
            CreatureSize.Large,
            CreatureType.Ooze,
            "Unaligned",
            2.0,
            15,
            new AbilityScores(14, 3, 20, 1, 6, 1),
            new HitPoints(84, 84),
            new ArmorClass(6, 0, false));
        cube.AddAction(new Action(cube.Id, "Pseudopod", ActionType.Action, "Golpe ácido con masa gelatinosa translúcida.", true, 5, "3d6", DamageType.Acid, AbilityScoreType.Strength));
        creatures.Add(cube);

        db.Creatures.AddRange(creatures);
        db.SaveChanges();
    }
}
