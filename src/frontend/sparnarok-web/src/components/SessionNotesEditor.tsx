import React, { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../services/apiClient';
import type { SessionNoteResponseDto } from '../types/liveCombat';

export interface SessionNotesEditorProps {
  partyId?: string | null;
}

/**
 * Editor y visualizador de notas de sesión en formato Markdown exclusivo para el Dungeon Master.
 * Permite documentar el lore, trampas, secretos y recompensas de la campaña con guardado en vivo.
 */
export const SessionNotesEditor: React.FC<SessionNotesEditorProps> = ({ partyId }) => {
  const { t } = useTranslation();

  const isQuickMode = !partyId || partyId === 'quick-session';

  const [notes, setNotes] = useState<SessionNoteResponseDto[]>([]);
  const [selectedNoteId, setSelectedNoteId] = useState<string | null>(null);
  const [title, setTitle] = useState<string>('');
  const [content, setContent] = useState<string>('');
  const [savedTitle, setSavedTitle] = useState<string>('');
  const [savedContent, setSavedContent] = useState<string>('');
  const [previewMode, setPreviewMode] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const loadNotes = useCallback(async () => {
    setIsLoading(true);
    try {
      if (isQuickMode) {
        if (typeof window !== 'undefined') {
          const raw = window.localStorage.getItem('sparnarok_session_notes_quick');
          const localNotes: SessionNoteResponseDto[] = raw ? JSON.parse(raw) : [];
          setNotes(localNotes);
          if (localNotes.length > 0) {
            setSelectedNoteId((prevId) => {
              const active = localNotes.find((n) => n.id === prevId) || localNotes[0];
              setTitle(active.title);
              setContent(active.content);
              setSavedTitle(active.title);
              setSavedContent(active.content);
              return active.id;
            });
          }
        }
        return;
      }

      if (partyId) {
        const data = await apiClient.getSessionNotes(partyId);
        setNotes(data);
        if (data.length > 0) {
          setSelectedNoteId((prevId) => {
            const active = data.find((n) => n.id === prevId) || data[0];
            setTitle(active.title);
            setContent(active.content);
            setSavedTitle(active.title);
            setSavedContent(active.content);
            return active.id;
          });
        }
      }
    } catch (err) {
      console.error('[SessionNotes] Error cargando notas:', err);
    } finally {
      setIsLoading(false);
    }
  }, [partyId, isQuickMode]);

  useEffect(() => {
    loadNotes();
  }, [loadNotes]);

  const handleSelectNote = (note: SessionNoteResponseDto) => {
    setSelectedNoteId(note.id);
    setTitle(note.title);
    setContent(note.content);
    setSavedTitle(note.title);
    setSavedContent(note.content);
    setStatusMessage(null);
  };

  const handleNewNote = () => {
    setSelectedNoteId(null);
    setTitle('');
    setContent('');
    setSavedTitle('');
    setSavedContent('');
    setPreviewMode(false);
    setStatusMessage(null);
  };

  const handleSaveNote = async () => {
    if (!title.trim()) return;

    setIsLoading(true);
    try {
      if (isQuickMode) {
        const now = new Date().toISOString();
        if (selectedNoteId) {
          const updated = notes.map((n) =>
            n.id === selectedNoteId ? { ...n, title: title.trim(), content: content.trim(), updatedAt: now } : n
          );
          setNotes(updated);
          window.localStorage.setItem('sparnarok_session_notes_quick', JSON.stringify(updated));
        } else {
          const newNote: SessionNoteResponseDto = {
            id: typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `local-${Date.now()}`,
            partyId: 'quick-session',
            ownerUserId: 'local',
            title: title.trim(),
            content: content.trim(),
            createdAt: now,
            updatedAt: now,
          };
          const updated = [newNote, ...notes];
          setNotes(updated);
          setSelectedNoteId(newNote.id);
          window.localStorage.setItem('sparnarok_session_notes_quick', JSON.stringify(updated));
        }
        setSavedTitle(title.trim());
        setSavedContent(content.trim());
        setStatusMessage(t('liveCombat.savedSuccess', '¡Nota guardada exitosamente!'));
        return;
      }

      if (!partyId) return;

      if (selectedNoteId) {
        const updated = await apiClient.updateSessionNote(partyId, selectedNoteId, {
          title: title.trim(),
          content: content.trim(),
        });
        setNotes((prev) => prev.map((n) => (n.id === updated.id ? updated : n)));
        setSavedTitle(updated.title);
        setSavedContent(updated.content);
        setStatusMessage(t('liveCombat.savedSuccess', '¡Nota guardada exitosamente!'));
      } else {
        const created = await apiClient.createSessionNote(partyId, {
          title: title.trim(),
          content: content.trim(),
        });
        setNotes((prev) => [created, ...prev]);
        setSelectedNoteId(created.id);
        setSavedTitle(created.title);
        setSavedContent(created.content);
        setStatusMessage(t('liveCombat.savedSuccess', '¡Nota guardada exitosamente!'));
      }
    } catch (err) {
      console.error('[SessionNotes] Error guardando nota:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleDeleteNote = async (noteId: string) => {
    if (!window.confirm(t('liveCombat.confirmDeleteNote', '¿Estás seguro de eliminar esta nota de sesión?'))) {
      return;
    }

    try {
      if (isQuickMode) {
        const remaining = notes.filter((n) => n.id !== noteId);
        setNotes(remaining);
        window.localStorage.setItem('sparnarok_session_notes_quick', JSON.stringify(remaining));
        if (remaining.length > 0) {
          setSelectedNoteId(remaining[0].id);
          setTitle(remaining[0].title);
          setContent(remaining[0].content);
        } else {
          handleNewNote();
        }
        return;
      }

      if (!partyId) return;

      await apiClient.deleteSessionNote(partyId, noteId);
      const remaining = notes.filter((n) => n.id !== noteId);
      setNotes(remaining);
      if (remaining.length > 0) {
        setSelectedNoteId(remaining[0].id);
        setTitle(remaining[0].title);
        setContent(remaining[0].content);
      } else {
        handleNewNote();
      }
    } catch (err) {
      console.error('[SessionNotes] Error eliminando nota:', err);
    }
  };

  /**
   * Renderiza Markdown simple de forma segura sin librerías externas pesadas.
   */
  const renderMarkdownPreview = (text: string) => {
    if (!text.trim()) {
      return <p className="markdown-empty-hint">{t('liveCombat.noteContentPlaceholder', 'Escribe tus notas en formato Markdown...')}</p>;
    }

    const lines = text.split('\n');
    return lines.map((line, idx) => {
      const trimmed = line.trim();
      if (trimmed.startsWith('### ')) {
        return <h3 key={idx} className="md-h3">{trimmed.replace('### ', '')}</h3>;
      }
      if (trimmed.startsWith('## ')) {
        return <h2 key={idx} className="md-h2">{trimmed.replace('## ', '')}</h2>;
      }
      if (trimmed.startsWith('# ')) {
        return <h1 key={idx} className="md-h1">{trimmed.replace('# ', '')}</h1>;
      }
      if (trimmed.startsWith('- ') || trimmed.startsWith('* ')) {
        return <li key={idx} className="md-li">{trimmed.substring(2)}</li>;
      }
      if (trimmed.startsWith('> ')) {
        return <blockquote key={idx} className="md-blockquote">{trimmed.substring(2)}</blockquote>;
      }
      if (trimmed === '') {
        return <div key={idx} className="md-spacer" style={{ height: '8px' }} />;
      }
      return <p key={idx} className="md-p">{trimmed}</p>;
    });
  };

  const isDirty = selectedNoteId
    ? title.trim() !== savedTitle.trim() || content.trim() !== savedContent.trim()
    : title.trim().length > 0 || content.trim().length > 0;

  return (
    <div className="session-notes-container" id="session-notes-editor-panel">
      <div className="session-notes-sidebar">
        <div className="notes-sidebar-header">
          <div className="notes-title-group">
            <span className="notes-sidebar-title">{t('liveCombat.notesTitle', 'Mis Notas')}</span>
            <span className="notes-privacy-tag" title={isQuickMode ? 'Guardado localmente en este dispositivo' : 'Estas notas son privadas y solo tú puedes leerlas'}>
              {isQuickMode ? '⚡ Local' : '🔒 Privadas'}
            </span>
          </div>
          <button
            type="button"
            id="new-session-note-btn"
            className="btn btn-secondary btn-sm"
            onClick={handleNewNote}
          >
            {t('liveCombat.newNoteBtn', '+ Nueva Nota')}
          </button>
        </div>

        {notes.length === 0 ? (
          <div className="notes-empty-sidebar">
            <p>{t('liveCombat.noNotes', 'No hay notas registradas.')}</p>
          </div>
        ) : (
          <ul className="notes-list" role="list">
            {notes.map((note) => (
              <li
                key={note.id}
                className={`note-list-item ${note.id === selectedNoteId ? 'active' : ''}`}
                onClick={() => handleSelectNote(note)}
                role="button"
                tabIndex={0}
                onKeyDown={(e) => e.key === 'Enter' && handleSelectNote(note)}
              >
                <div className="note-item-title">{note.title}</div>
                <div className="note-item-date">
                  {new Date(note.updatedAt || note.createdAt).toLocaleDateString()}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="session-notes-workspace">
        <div className="notes-workspace-toolbar">
          <input
            type="text"
            id="session-note-title-input"
            className="note-title-input"
            placeholder={t('liveCombat.noteTitlePlaceholder', 'Título de la nota...')}
            value={title}
            onChange={(e) => setTitle(e.target.value)}
          />

          <div className="notes-toolbar-actions">
            <button
              type="button"
              id="toggle-markdown-preview-btn"
              className="btn btn-outline btn-sm"
              onClick={() => setPreviewMode((prev) => !prev)}
            >
              {previewMode
                ? `✏️ ${t('liveCombat.editTab', 'Editar')}`
                : `👁️ ${t('liveCombat.previewTab', 'Vista Previa')}`}
            </button>
            <button
              type="button"
              id="save-session-note-btn"
              className="btn btn-primary btn-sm"
              onClick={handleSaveNote}
              disabled={isLoading || !title.trim() || (!isDirty && !!selectedNoteId)}
            >
              {isLoading
                ? `⏳ ${t('common.saving', 'Guardando...')}`
                : !isDirty && selectedNoteId
                ? `✓ ${t('liveCombat.noteSavedBadge', 'Nota guardada')}`
                : `💾 ${t('liveCombat.saveNoteBtn', 'Guardar')}`}
            </button>
            {selectedNoteId && (
              <button
                type="button"
                id="delete-session-note-btn"
                className="btn btn-danger btn-sm"
                onClick={() => handleDeleteNote(selectedNoteId)}
              >
                🗑️ {t('liveCombat.deleteNoteBtn', 'Eliminar')}
              </button>
            )}
          </div>
        </div>

        {statusMessage && (
          <div className="note-status-banner" role="status">
            {statusMessage}
          </div>
        )}

        <div className="notes-workspace-body">
          {previewMode ? (
            <div className="markdown-preview-box" id="markdown-preview-box">
              {renderMarkdownPreview(content)}
            </div>
          ) : (
            <textarea
              id="session-note-content-input"
              className="markdown-textarea"
              placeholder={t('liveCombat.noteContentPlaceholder', 'Escribe tus notas en formato Markdown...')}
              value={content}
              onChange={(e) => setContent(e.target.value)}
              rows={16}
            />
          )}
        </div>
      </div>
    </div>
  );
};
