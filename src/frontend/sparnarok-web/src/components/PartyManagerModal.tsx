import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../services/apiClient';
import type { PartyResponseDto, CreatePartyRequestDto } from '../types/partyEncounter';
import type { PartyRole } from '../types/liveCombat';

export interface PartyManagerModalProps {
  isOpen: boolean;
  onClose: () => void;
  parties: PartyResponseDto[];
  currentUserId?: string | null;
  currentUserDisplayName?: string | null;
  onPartyCreated?: (party: PartyResponseDto) => void;
  onPartyDeleted?: (partyId: string) => void;
  onOpenLiveSession?: (partyId: string) => void;
}

/**
 * Modal dedicado para la gestión y CRUD básico de Partys de juego.
 * Permite visualizar grupos activos, crear una nueva Party especificando rol y apodo inicial,
 * copiar el enlace de invitación para jugadores e iniciar el Modo Partida.
 */
export const PartyManagerModal: React.FC<PartyManagerModalProps> = ({
  isOpen,
  onClose,
  parties,
  currentUserId,
  currentUserDisplayName,
  onPartyCreated,
  onPartyDeleted,
  onOpenLiveSession,
}) => {
  const { t } = useTranslation();

  const [isCreating, setIsCreating] = useState(false);
  const [newPartyName, setNewPartyName] = useState('');
  const [creatorRole, setCreatorRole] = useState<PartyRole>('DungeonMaster');
  const [creatorInGameName, setCreatorInGameName] = useState(
    currentUserDisplayName || ''
  );
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [copiedPartyId, setCopiedPartyId] = useState<string | null>(null);
  const [deletingPartyId, setDeletingPartyId] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newPartyName.trim()) {
      setErrorMessage(t('parties.nameRequired', 'El nombre de la Party es obligatorio.'));
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);

    try {
      const payload: CreatePartyRequestDto = {
        name: newPartyName.trim(),
        memberCreatureIds: [],
        role: creatorRole,
        inGameName: creatorInGameName.trim() || (creatorRole === 'DungeonMaster' ? 'Dungeon Master' : 'Aventurero'),
      };

      const createdParty = await apiClient.createParty(payload);
      onPartyCreated?.(createdParty);
      setNewPartyName('');
      setCreatorInGameName(currentUserDisplayName || '');
      setIsCreating(false);
    } catch (err: any) {
      setErrorMessage(
        err?.message || t('parties.createError', 'Error al crear la Party. Inténtalo de nuevo.')
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCopyInviteLink = (party: PartyResponseDto) => {
    if (!party.inviteToken) return;
    const url = `${window.location.origin}${window.location.pathname}?inviteToken=${party.inviteToken}`;
    navigator.clipboard?.writeText(url);
    setCopiedPartyId(party.id);
    setTimeout(() => {
      setCopiedPartyId(null);
    }, 2500);
  };

  const handleDelete = async (partyId: string) => {
    const confirmDelete = window.confirm(
      t('parties.deletePartyConfirm', '¿Estás seguro de eliminar esta Party?')
    );
    if (!confirmDelete) return;

    setDeletingPartyId(partyId);
    try {
      await apiClient.deleteParty(partyId);
      onPartyDeleted?.(partyId);
    } catch (err: any) {
      alert(err?.message || 'Error al eliminar la Party.');
    } finally {
      setDeletingPartyId(null);
    }
  };

  return (
    <div className="modal-backdrop" id="party-manager-modal-backdrop">
      <div
        className="modal-content party-manager-modal-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="party-manager-modal-title"
      >
        <div className="modal-header">
          <div className="modal-header-left">
            <span className="modal-icon">🛡️</span>
            <div>
              <h2 className="modal-title" id="party-manager-modal-title">
                {t('parties.myPartiesTitle', 'Mis Partys de Juego')}
              </h2>
              <p className="modal-subtitle">
                {t(
                  'parties.myPartiesSubtitle',
                  'Crea y administra tus grupos, copia enlaces de invitación o inicia el Modo Partida.'
                )}
              </p>
            </div>
          </div>
          <button
            type="button"
            id="close-parties-modal-btn"
            className="modal-close-btn"
            onClick={onClose}
            aria-label="Cerrar modal"
          >
            ✕
          </button>
        </div>

        <div className="modal-body party-manager-modal-body">
          {/* Barra de acción superior: Alternar creación */}
          <div className="party-manager-toolbar">
            <button
              type="button"
              id="toggle-create-party-btn"
              className={`btn btn-sm ${isCreating ? 'btn-secondary' : 'btn-primary primary-gold'}`}
              onClick={() => setIsCreating(!isCreating)}
            >
              {isCreating ? '✕ Cancelar Creación' : `+ ${t('parties.createNewParty', 'Crear Nueva Party')}`}
            </button>
          </div>

          {/* Formulario de creación de Party */}
          {isCreating && (
            <form
              onSubmit={handleCreateSubmit}
              className="party-create-form-box fade-in"
              id="party-create-form"
            >
              <h3>{t('parties.createNewParty', 'Crear Nueva Party')}</h3>

              {errorMessage && (
                <div className="form-error-banner" role="alert">
                  ⚠️ {errorMessage}
                </div>
              )}

              <div className="form-group">
                <label htmlFor="new-party-name-input">
                  {t('parties.partyName', 'Nombre de la Party')} *
                </label>
                <input
                  type="text"
                  id="new-party-name-input"
                  className="form-input"
                  value={newPartyName}
                  onChange={(e) => setNewPartyName(e.target.value)}
                  placeholder={t(
                    'parties.partyNamePlaceholder',
                    'ej. La Compañía del Dragón'
                  )}
                  required
                />
              </div>

              <div className="form-row-dual">
                <div className="form-group">
                  <label htmlFor="new-party-role-select">
                    {t('parties.creatorRole', 'Mi Rol Inicial')}
                  </label>
                  <select
                    id="new-party-role-select"
                    className="form-select"
                    value={creatorRole}
                    onChange={(e) => setCreatorRole(e.target.value as PartyRole)}
                  >
                    <option value="DungeonMaster">
                      👑 {t('parties.roleDm', 'Dungeon Master')}
                    </option>
                    <option value="Player">
                      🛡️ {t('parties.rolePlayer', 'Jugador')}
                    </option>
                  </select>
                </div>

                <div className="form-group">
                  <label htmlFor="new-party-ingame-name-input">
                    {t('parties.creatorInGameName', 'Mi Apodo / Nombre en Juego')}
                  </label>
                  <input
                    type="text"
                    id="new-party-ingame-name-input"
                    className="form-input"
                    value={creatorInGameName}
                    onChange={(e) => setCreatorInGameName(e.target.value)}
                    placeholder={t(
                      'parties.creatorInGameNamePlaceholder',
                      'ej. Gandalf o Dungeon Master'
                    )}
                  />
                </div>
              </div>

              <div className="form-actions-right">
                <button
                  type="submit"
                  id="create-party-submit-btn"
                  className="btn btn-primary primary-gold"
                  disabled={isSubmitting || !newPartyName.trim()}
                >
                  {isSubmitting ? 'Guardando...' : `✨ ${t('parties.createPartySubmit', 'Crear Party')}`}
                </button>
              </div>
            </form>
          )}

          {/* Listado de Partys */}
          <div className="parties-list-container" id="user-parties-list">
            {parties.length === 0 ? (
              <div className="parties-empty-state" id="no-parties-alert">
                <div className="empty-icon">🛡️</div>
                <p>
                  {t(
                    'parties.noPartiesFound',
                    'No perteneces a ninguna Party todavía. ¡Crea una nueva o únete con un enlace de invitación!'
                  )}
                </p>
              </div>
            ) : (
              parties.map((party) => {
                const isOwner = currentUserId && party.ownerUserId === currentUserId;
                const membersCount =
                  party.members?.length || party.partyMembers?.length || 0;
                const userMembership = party.partyMembers?.find(
                  (m) => m.userId === currentUserId
                );
                const roleDisplay = userMembership?.role || (isOwner ? 'DungeonMaster' : 'Player');

                return (
                  <div
                    key={party.id}
                    className="party-card"
                    id={`party-card-${party.id}`}
                    data-testid={`party-card-${party.id}`}
                  >
                    <div className="party-card-info">
                      <div className="party-card-title-row">
                        <h4 className="party-card-title">{party.name}</h4>
                        <span
                          className={`party-role-badge role-${roleDisplay.toLowerCase()}`}
                        >
                          {roleDisplay === 'DungeonMaster'
                            ? `👑 ${t('parties.roleDm', 'Dungeon Master')}`
                            : `🛡️ ${t('parties.rolePlayer', 'Jugador')}`}
                        </span>
                        {isOwner && (
                          <span className="party-owner-pill">
                            {t('parties.ownerBadge', 'Creador')}
                          </span>
                        )}
                      </div>

                      <div className="party-card-meta">
                        <span>
                          👥{' '}
                          {t('parties.membersCount', {
                            count: membersCount,
                            defaultValue: `${membersCount} miembros`,
                          })}
                        </span>
                        {party.createdAt && (
                          <span>
                            📅 {new Date(party.createdAt).toLocaleDateString()}
                          </span>
                        )}
                      </div>
                    </div>

                    <div className="party-card-actions">
                      {party.inviteToken && (
                        <button
                          type="button"
                          id={`copy-invite-link-${party.id}`}
                          className="btn btn-outline btn-xs copy-invite-btn"
                          onClick={() => handleCopyInviteLink(party)}
                          title={`Token: ${party.inviteToken}`}
                        >
                          {copiedPartyId === party.id
                            ? t('parties.inviteLinkCopied', '✓ Enlace Copiado')
                            : `🔗 ${t('parties.copyInviteLink', 'Copiar Enlace')}`}
                        </button>
                      )}

                      <button
                        type="button"
                        id={`launch-live-session-${party.id}`}
                        className="btn btn-primary btn-xs primary-gold"
                        onClick={() => {
                          onOpenLiveSession?.(party.id);
                          onClose();
                        }}
                      >
                        ⚔️ {t('parties.openLiveSession', 'Modo Partida')}
                      </button>

                      {isOwner && (
                        <button
                          type="button"
                          id={`delete-party-${party.id}`}
                          className="btn btn-danger btn-xs"
                          disabled={deletingPartyId === party.id}
                          onClick={() => handleDelete(party.id)}
                          aria-label={t('parties.deleteParty', 'Eliminar')}
                          title={t('parties.deleteParty', 'Eliminar')}
                        >
                          🗑️
                        </button>
                      )}
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
