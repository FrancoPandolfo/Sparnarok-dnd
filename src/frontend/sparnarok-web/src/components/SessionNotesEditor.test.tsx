import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { SessionNotesEditor } from './SessionNotesEditor';
import { apiClient } from '../services/apiClient';

vi.mock('../services/apiClient', () => ({
  apiClient: {
    getSessionNotes: vi.fn(),
    createSessionNote: vi.fn(),
    updateSessionNote: vi.fn(),
    deleteSessionNote: vi.fn(),
  },
}));

describe('SessionNotesEditor Component Suite', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  const mockNotes = [
    {
      id: 'note-1',
      partyId: 'party-1',
      title: 'Sesión 1: Emboscada',
      content: '# Lore\n- Pista 1\n> Cuidado con los trasgos',
      createdAt: '2026-10-22T20:00:00Z',
      updatedAt: '2026-10-22T20:00:00Z',
    },
  ];

  it('renders notes list and loads active note content', async () => {
    vi.mocked(apiClient.getSessionNotes).mockResolvedValue(mockNotes);

    render(<SessionNotesEditor partyId="party-1" />);

    await waitFor(() => {
      expect(screen.getByText('Sesión 1: Emboscada')).toBeInTheDocument();
    });

    const titleInput = screen.getByPlaceholderText(/Título de la nota/i);
    expect(titleInput).toHaveValue('Sesión 1: Emboscada');
  });

  it('allows editing title, content and saving a note', async () => {
    vi.mocked(apiClient.getSessionNotes).mockResolvedValue([]);
    vi.mocked(apiClient.createSessionNote).mockResolvedValue({
      id: 'note-new',
      partyId: 'party-1',
      title: 'Nueva Sesión',
      content: 'Contenido nuevo',
      createdAt: '2026-10-22T21:00:00Z',
      updatedAt: '2026-10-22T21:00:00Z',
    });

    render(<SessionNotesEditor partyId="party-1" />);

    // Wait for initial load to finish so button is enabled
    await waitFor(() => {
      expect(screen.getByText(/No hay notas registradas/i)).toBeInTheDocument();
    });

    const titleInput = screen.getByPlaceholderText(/Título de la nota/i);
    const contentTextarea = screen.getByPlaceholderText(/Escribe tus notas en formato Markdown/i);

    fireEvent.change(titleInput, { target: { value: 'Nueva Sesión' } });
    fireEvent.change(contentTextarea, { target: { value: 'Contenido nuevo' } });

    const saveBtn = screen.getByRole('button', { name: /Guardar/i });
    expect(saveBtn).not.toBeDisabled();
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(apiClient.createSessionNote).toHaveBeenCalledWith('party-1', {
        title: 'Nueva Sesión',
        content: 'Contenido nuevo',
      });
    });
  });

  it('toggles markdown preview mode', async () => {
    vi.mocked(apiClient.getSessionNotes).mockResolvedValue(mockNotes);

    render(<SessionNotesEditor partyId="party-1" />);

    await waitFor(() => {
      expect(screen.getByText('Sesión 1: Emboscada')).toBeInTheDocument();
    });

    const previewToggleBtn = screen.getByRole('button', { name: /Vista Previa/i });
    fireEvent.click(previewToggleBtn);

    // After clicking Vista Previa, markdown should render formatted heading
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Lore');
    expect(screen.getByRole('button', { name: /Editar/i })).toBeInTheDocument();
  });
});
