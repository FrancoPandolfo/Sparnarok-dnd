import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../services/apiClient';
import { useAuth } from '../context/AuthContext';
import type { PartyInviteDetailsDto, PartyResponseDto } from '../types/partyEncounter';

export interface PartyOnboardingModalProps {
  isOpen: boolean;
  inviteToken?: string | null;
  onClose: () => void;
  onJoinedSuccess: (party: PartyResponseDto) => void;
}

/**
 * Modal de Onboarding para unirse a una Party mediante token de invitación.
 * Permite al aventurero o Dungeon Master configurar su identidad local (Role e InGameName)
 * antes de ingresar a la campaña.
 */
export const PartyOnboardingModal: React.FC<PartyOnboardingModalProps> = ({
  isOpen,
  inviteToken: initialInviteToken,
  onClose,
  onJoinedSuccess,
}) => {
  const { t } = useTranslation();
  const { user, isAuthenticated } = useAuth();

  const [tokenInput, setTokenInput] = useState<string>(initialInviteToken || '');
  const [activeToken, setActiveToken] = useState<string | null>(initialInviteToken || null);
  const [inviteDetails, setInviteDetails] = useState<PartyInviteDetailsDto | null>(null);
  const [isLoadingDetails, setIsLoadingDetails] = useState<boolean>(false);
  const [role, setRole] = useState<'DungeonMaster' | 'Player'>('Player');
  const [inGameName, setInGameName] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  useEffect(() => {
    if (initialInviteToken) {
      setTokenInput(initialInviteToken);
      setActiveToken(initialInviteToken);
    }
  }, [initialInviteToken]);

  // Sincronizar InGameName por defecto con GlobalNickname o DisplayName del usuario autenticado
  useEffect(() => {
    if (user && !inGameName) {
      setInGameName(user.globalNickname || user.displayName || '');
    }
  }, [user, inGameName]);

  // Cargar detalles de la invitación cuando hay un token activo
  useEffect(() => {
    if (!isOpen || !activeToken) {
      setInviteDetails(null);
      return;
    }

    let isMounted = true;
    const fetchDetails = async () => {
      setIsLoadingDetails(true);
      setErrorMessage(null);
      try {
        const details = await apiClient.getPartyInviteDetails(activeToken.trim());
        if (isMounted) {
          setInviteDetails(details);
        }
      } catch (err: any) {
        if (isMounted) {
          setErrorMessage(err.message || 'Token de invitación inválido o expirado.');
          setInviteDetails(null);
        }
      } finally {
        if (isMounted) {
          setIsLoadingDetails(false);
        }
      }
    };

    fetchDetails();
    return () => {
      isMounted = false;
    };
  }, [isOpen, activeToken]);

  if (!isOpen) return null;

  const handleVerifyToken = (e: React.FormEvent) => {
    e.preventDefault();
    const cleaned = tokenInput.trim();
    if (!cleaned) return;
    setActiveToken(cleaned);
  };

  const handleJoinParty = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeToken) return;

    if (!inGameName.trim()) {
      setErrorMessage('Debes especificar tu nombre de personaje o apodo en la partida.');
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);
    try {
      const party = await apiClient.joinPartyByToken(activeToken.trim(), {
        role,
        inGameName: inGameName.trim(),
      });
      setSuccessMessage(`¡Te has unido exitosamente a "${party.name}" como ${inGameName}!`);
      setTimeout(() => {
        onJoinedSuccess(party);
        onClose();
      }, 1200);
    } catch (err: any) {
      setErrorMessage(err.message || 'Error al unirse a la party.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop" id="party-onboarding-modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal-card party-onboarding-card fade-in">
        <div className="modal-header">
          <div className="modal-header-icon">🛡️</div>
          <h3 className="modal-title" id="party-onboarding-title">
            Unirse a la Party (Onboarding)
          </h3>
          <button
            type="button"
            id="close-onboarding-modal-btn"
            className="modal-close-btn"
            onClick={onClose}
            aria-label="Cerrar modal"
          >
            ✕
          </button>
        </div>

        <div className="modal-body">
          {/* Mensajes de Alerta */}
          {errorMessage && (
            <div className="auth-alert error-alert" role="alert">
              ⚠️ {errorMessage}
            </div>
          )}
          {successMessage && (
            <div className="auth-alert success-alert" role="alert">
              ✓ {successMessage}
            </div>
          )}

          {!isAuthenticated && (
            <div className="auth-alert error-alert" role="alert">
              🔒 Debes iniciar sesión con tu cuenta antes de unirte a una Party.
            </div>
          )}

          {/* Formulario de ingreso de token manual si no se proporcionó uno */}
          {!activeToken ? (
            <form onSubmit={handleVerifyToken} className="modal-form">
              <div className="field-group">
                <label htmlFor="invite-token-input" className="field-label">
                  Token o Enlace de Invitación:
                </label>
                <input
                  id="invite-token-input"
                  type="text"
                  className="input-text"
                  placeholder="Ej: a1b2c3d4-e5f6-7890..."
                  value={tokenInput}
                  onChange={(e) => setTokenInput(e.target.value)}
                  required
                  autoFocus
                />
              </div>
              <div className="modal-actions">
                <button
                  type="button"
                  id="cancel-token-input-btn"
                  className="btn btn-secondary"
                  onClick={onClose}
                >
                  {t('common.cancel', 'Cancelar')}
                </button>
                <button
                  type="submit"
                  id="verify-token-btn"
                  className="btn btn-primary primary-gold"
                  disabled={!tokenInput.trim()}
                >
                  🔍 Verificar Invitación
                </button>
              </div>
            </form>
          ) : isLoadingDetails ? (
            <div className="loading-spinner-box">
              <p>⏳ Verificando credenciales de la Party...</p>
            </div>
          ) : inviteDetails ? (
            <form onSubmit={handleJoinParty} className="modal-form" id="party-onboarding-form">
              <div className="party-invite-banner">
                <div className="invite-banner-details">
                  <span className="party-badge">Campaña Encontrada</span>
                  <h4 className="party-invite-name" id="invited-party-name">{inviteDetails.partyName}</h4>
                  <p className="party-invite-creator">
                    Miembros actuales: <strong>{inviteDetails.memberCount}</strong>
                  </p>
                </div>
              </div>

              <div className="field-group">
                <label htmlFor="onboarding-role-select" className="field-label">
                  ¿Cuál será tu Rol en la Party?
                </label>
                <select
                  id="onboarding-role-select"
                  className="input-text"
                  value={role}
                  onChange={(e) => setRole(e.target.value as 'DungeonMaster' | 'Player')}
                >
                  <option value="Player">🛡️ Jugador (Player)</option>
                  <option value="DungeonMaster">👑 Dungeon Master (DM)</option>
                </select>
              </div>

              <div className="field-group">
                <label htmlFor="onboarding-ingame-name" className="field-label">
                  Nombre en Partida (InGame Name):
                </label>
                <input
                  id="onboarding-ingame-name"
                  type="text"
                  className="input-text"
                  placeholder="Ej: Elendur, Thorin, o tu apodo de DM"
                  value={inGameName}
                  onChange={(e) => setInGameName(e.target.value)}
                  maxLength={50}
                  required
                />
                <span className="field-hint">
                  Este es el nombre visible para los miembros de esta party específica.
                </span>
              </div>

              <div className="modal-actions">
                <button
                  type="button"
                  id="cancel-onboarding-btn"
                  className="btn btn-secondary"
                  onClick={onClose}
                  disabled={isSubmitting}
                >
                  {t('common.cancel', 'Cancelar')}
                </button>
                <button
                  type="submit"
                  id="confirm-join-party-btn"
                  className="btn btn-primary primary-gold"
                  disabled={isSubmitting || !inGameName.trim() || !isAuthenticated}
                >
                  {isSubmitting ? 'Uniéndote...' : '⚔️ Unirse a la Party'}
                </button>
              </div>
            </form>
          ) : (
            <div className="modal-actions">
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => {
                  setActiveToken(null);
                  setTokenInput('');
                }}
              >
                ← Probar con otro Token
              </button>
              <button
                type="button"
                className="btn btn-outline"
                onClick={onClose}
              >
                Cerrar
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
