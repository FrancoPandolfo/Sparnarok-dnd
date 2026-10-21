import React, { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  CreatureResponseDto,
  WeaponDto,
  MontecarloRequestDto,
  MontecarloResultDto,
} from '../types/dnd';

export interface CombatSimulatorViewProps {
  creatures: CreatureResponseDto[];
  weapons: WeaponDto[];
  onRunSimulation: (request: MontecarloRequestDto) => Promise<MontecarloResultDto>;
  onSimulationCompleted?: (result: MontecarloResultDto) => void;
}

/**
 * Vista de Simulación Táctica de Montecarlo (1v1).
 * Permite seleccionar combatientes y armas, definir el volumen de iteraciones
 * y ejecutar duelos concurrentes en paralelo visualizando win-rate y métricas agregadas.
 */
export const CombatSimulatorView: React.FC<CombatSimulatorViewProps> = ({
  creatures,
  weapons,
  onRunSimulation,
  onSimulationCompleted,
}) => {
  const { t } = useTranslation();

  const [combatantAId, setCombatantAId] = useState<string>(() => creatures[0]?.id || '');
  const [weaponAId, setWeaponAId] = useState<string>(() => weapons[0]?.id || '');

  const [combatantBId, setCombatantBId] = useState<string>(
    () => (creatures.length > 1 ? creatures[1].id : creatures[0]?.id || '')
  );
  const [weaponBId, setWeaponBId] = useState<string>(
    () => (weapons.length > 1 ? weapons[1].id : weapons[0]?.id || '')
  );

  // Sincronizar selección inicial cuando las criaturas o armas cargan asíncronamente
  React.useEffect(() => {
    if (!combatantAId && creatures.length > 0) {
      setCombatantAId(creatures[0].id);
    }
    if (!combatantBId && creatures.length > 0) {
      setCombatantBId(creatures.length > 1 ? creatures[1].id : creatures[0].id);
    }
  }, [creatures, combatantAId, combatantBId]);

  React.useEffect(() => {
    if (!weaponAId && weapons.length > 0) {
      setWeaponAId(weapons[0].id);
    }
    if (!weaponBId && weapons.length > 0) {
      setWeaponBId(weapons.length > 1 ? weapons[1].id : weapons[0].id);
    }
  }, [weapons, weaponAId, weaponBId]);

  const [iterations, setIterations] = useState<number>(10000);
  const [isSimulating, setIsSimulating] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [result, setResult] = useState<MontecarloResultDto | null>(null);

  // Criaturas seleccionadas para vista previa de estadísticas
  const creatureA = useMemo(
    () => creatures.find((c) => c.id === combatantAId) || creatures[0],
    [creatures, combatantAId]
  );
  const creatureB = useMemo(
    () => creatures.find((c) => c.id === combatantBId) || creatures[1] || creatures[0],
    [creatures, combatantBId]
  );

  const handleRun = async () => {
    if (!combatantAId || !combatantBId || !weaponAId || !weaponBId) {
      return;
    }

    setIsSimulating(true);
    setErrorMessage(null);

    try {
      const response = await onRunSimulation({
        combatantAId,
        weaponAId,
        combatantBId,
        weaponBId,
        iterations,
      });

      setResult(response);
      onSimulationCompleted?.(response);
    } catch {
      setErrorMessage(t('simulation.errorSimulating'));
    } finally {
      setIsSimulating(false);
    }
  };

  return (
    <div className="combat-simulator-view fade-in">
      <header className="simulator-header">
        <h2 className="section-title">{t('simulation.title')}</h2>
        <p className="section-subtitle">{t('simulation.subtitle')}</p>
      </header>

      {/* Panel de Configuración de Combatientes */}
      <div className="sim-setup-grid">
        {/* Combatiente A */}
        <div className="combatant-config-card initiator-card">
          <div className="card-badge initiator-badge">{t('simulation.combatantA')}</div>
          <div className="field-group">
            <label htmlFor="combatant-a-select" className="field-label">
              {t('simulation.creatureSelect')}
            </label>
            <select
              id="combatant-a-select"
              className="sim-select"
              value={combatantAId}
              onChange={(e) => setCombatantAId(e.target.value)}
              disabled={isSimulating}
            >
              {creatures.map((c) => (
                <option key={`a-${c.id}`} value={c.id}>
                  {c.name} (CR {c.challengeRating} | CA {c.effectiveArmorClass} | {c.hitPoints.max} HP)
                </option>
              ))}
            </select>
          </div>

          <div className="field-group">
            <label htmlFor="weapon-a-select" className="field-label">
              {t('simulation.weaponSelect')}
            </label>
            <select
              id="weapon-a-select"
              className="sim-select"
              value={weaponAId}
              onChange={(e) => setWeaponAId(e.target.value)}
              disabled={isSimulating}
            >
              {weapons.map((w) => (
                <option key={`w-a-${w.id}`} value={w.id}>
                  {w.name} ({w.damage} {w.damageType})
                </option>
              ))}
            </select>
          </div>

          {creatureA && (
            <div className="creature-preview-strip">
              <span>🛡️ CA: <strong>{creatureA.effectiveArmorClass}</strong></span>
              <span>❤️ HP: <strong>{creatureA.hitPoints.max}</strong></span>
              <span>⚡ PB: <strong>+{creatureA.proficiencyBonus}</strong></span>
            </div>
          )}
        </div>

        {/* Separador Táctico VS */}
        <div className="versus-divider">
          <span className="versus-icon">⚔️</span>
          <span className="versus-text">VS</span>
        </div>

        {/* Combatiente B */}
        <div className="combatant-config-card defender-card">
          <div className="card-badge defender-badge">{t('simulation.combatantB')}</div>
          <div className="field-group">
            <label htmlFor="combatant-b-select" className="field-label">
              {t('simulation.creatureSelect')}
            </label>
            <select
              id="combatant-b-select"
              className="sim-select"
              value={combatantBId}
              onChange={(e) => setCombatantBId(e.target.value)}
              disabled={isSimulating}
            >
              {creatures.map((c) => (
                <option key={`b-${c.id}`} value={c.id}>
                  {c.name} (CR {c.challengeRating} | CA {c.effectiveArmorClass} | {c.hitPoints.max} HP)
                </option>
              ))}
            </select>
          </div>

          <div className="field-group">
            <label htmlFor="weapon-b-select" className="field-label">
              {t('simulation.weaponSelect')}
            </label>
            <select
              id="weapon-b-select"
              className="sim-select"
              value={weaponBId}
              onChange={(e) => setWeaponBId(e.target.value)}
              disabled={isSimulating}
            >
              {weapons.map((w) => (
                <option key={`w-b-${w.id}`} value={w.id}>
                  {w.name} ({w.damage} {w.damageType})
                </option>
              ))}
            </select>
          </div>

          {creatureB && (
            <div className="creature-preview-strip">
              <span>🛡️ CA: <strong>{creatureB.effectiveArmorClass}</strong></span>
              <span>❤️ HP: <strong>{creatureB.hitPoints.max}</strong></span>
              <span>⚡ PB: <strong>+{creatureB.proficiencyBonus}</strong></span>
            </div>
          )}
        </div>
      </div>

      {/* Barra de Parámetros y Ejecución */}
      <div className="sim-controls-bar">
        <div className="iterations-control">
          <label htmlFor="iterations-select" className="field-label">
            {t('simulation.iterationsLabel')}
          </label>
          <select
            id="iterations-select"
            className="sim-select iterations-select"
            value={iterations}
            onChange={(e) => setIterations(Number(e.target.value))}
            disabled={isSimulating}
          >
            <option value={100}>100 duelos</option>
            <option value={1000}>1,000 duelos</option>
            <option value={5000}>5,000 duelos</option>
            <option value={10000}>10,000 duelos (Recomendado)</option>
            <option value={20000}>20,000 duelos (Stress test)</option>
          </select>
        </div>

        <button
          type="button"
          id="run-montecarlo-btn"
          className="run-simulation-btn"
          onClick={handleRun}
          disabled={isSimulating}
        >
          {isSimulating ? (
            <>
              <span className="spinner-inline" /> {t('simulation.simulating')}
            </>
          ) : (
            <>🎲 {t('simulation.runButton')}</>
          )}
        </button>
      </div>

      {/* Alerta de Error */}
      {errorMessage && (
        <div id="sim-error-message" className="sim-error-alert" role="alert">
          ⚠️ {errorMessage}
        </div>
      )}

      {/* Panel de Resultados de la Simulación */}
      {result && (
        <section id="montecarlo-results-panel" className="results-panel fade-in">
          <div className="results-header">
            <div>
              <h3 className="results-title">{t('simulation.resultsTitle')}</h3>
              <span className="matchup-subtitle">
                {result.combatantAName} ({result.weaponAName}) vs {result.combatantBName} ({result.weaponBName})
              </span>
            </div>
            <div id="sim-execution-time" className="exec-time-pill">
              ⚡ {t('simulation.executionTime', { ms: result.executionTimeMs })}
            </div>
          </div>

          {/* Gráfico de Barras de Win Rate */}
          <div className="winrate-section">
            <h4 className="winrate-title">{t('simulation.winRateTitle')}</h4>

            <div
              id="winrate-bar-container"
              className="winrate-bar-container"
              role="progressbar"
              aria-label="Win rate distribution"
              aria-valuenow={result.combatantAWinRate}
              aria-valuemin={0}
              aria-valuemax={100}
            >
              <div
                id="win-rate-a"
                className="winrate-segment segment-a"
                style={{ width: `${result.combatantAWinRate}%` }}
                title={`${result.combatantAName}: ${result.combatantAWinRate}%`}
              >
                {result.combatantAWinRate > 10 && (
                  <span className="segment-label">{result.combatantAWinRate}%</span>
                )}
              </div>

              {result.drawRate > 0 && (
                <div
                  id="draw-rate"
                  className="winrate-segment segment-draw"
                  style={{ width: `${result.drawRate}%` }}
                  title={`Draws: ${result.drawRate}%`}
                >
                  {result.drawRate > 10 && (
                    <span className="segment-label">{result.drawRate}%</span>
                  )}
                </div>
              )}

              <div
                id="win-rate-b"
                className="winrate-segment segment-b"
                style={{ width: `${result.combatantBWinRate}%` }}
                title={`${result.combatantBName}: ${result.combatantBWinRate}%`}
              >
                {result.combatantBWinRate > 10 && (
                  <span className="segment-label">{result.combatantBWinRate}%</span>
                )}
              </div>
            </div>

            {/* Resumen numérico debajo de la barra */}
            <div className="winrate-legend">
              <div id="win-rate-summary-a" className="legend-item legend-a">
                <span className="legend-color-dot dot-a" />
                <span className="legend-name">{result.combatantAName}:</span>
                <strong className="legend-value">
                  {result.combatantAWinRate}% ({result.combatantAWins.toLocaleString()} victorias)
                </strong>
              </div>

              {result.drawRate > 0 && (
                <div id="win-rate-summary-draws" className="legend-item legend-draw">
                  <span className="legend-color-dot dot-draw" />
                  <span className="legend-name">Empates:</span>
                  <strong className="legend-value">
                    {result.drawRate}% ({result.draws.toLocaleString()})
                  </strong>
                </div>
              )}

              <div id="win-rate-summary-b" className="legend-item legend-b">
                <span className="legend-color-dot dot-b" />
                <span className="legend-name">{result.combatantBName}:</span>
                <strong className="legend-value">
                  {result.combatantBWinRate}% ({result.combatantBWins.toLocaleString()} victorias)
                </strong>
              </div>
            </div>
          </div>

          {/* Grid de Métricas Agregadas */}
          <div id="metrics-grid-sim" className="metrics-grid-sim">
            {/* Promedio de Rondas */}
            <article className="sim-metric-card">
              <div className="metric-header">
                <span className="metric-icon">⏱️</span>
                <span className="metric-title">{t('simulation.avgRounds')}</span>
              </div>
              <div id="metric-avg-rounds" className="metric-value">
                {result.averageRounds}
              </div>
              <p className="metric-subtitle">{t('simulation.avgRoundsDesc')}</p>
            </article>

            {/* DPR Promedio Combatiente A */}
            <article className="sim-metric-card">
              <div className="metric-header">
                <span className="metric-icon">🗡️</span>
                <span className="metric-title">{t('simulation.dprCombatantA')}</span>
              </div>
              <div id="metric-dpr-a" className="metric-value">
                {result.combatantAAverageDpr}
              </div>
              <p className="metric-subtitle">
                {result.combatantAName} &bull; {t('simulation.dprDesc')}
              </p>
            </article>

            {/* DPR Promedio Combatiente B */}
            <article className="sim-metric-card">
              <div className="metric-header">
                <span className="metric-icon">🛡️</span>
                <span className="metric-title">{t('simulation.dprCombatantB')}</span>
              </div>
              <div id="metric-dpr-b" className="metric-value">
                {result.combatantBAverageDpr}
              </div>
              <p className="metric-subtitle">
                {result.combatantBName} &bull; {t('simulation.dprDesc')}
              </p>
            </article>

            {/* Total Duelos */}
            <article className="sim-metric-card">
              <div className="metric-header">
                <span className="metric-icon">🔄</span>
                <span className="metric-title">{t('simulation.totalIterations')}</span>
              </div>
              <div id="metric-total-iterations" className="metric-value">
                {result.totalIterations.toLocaleString()}
              </div>
              <p className="metric-subtitle">100% duelos concurrentes procesados</p>
            </article>
          </div>
        </section>
      )}
    </div>
  );
};
