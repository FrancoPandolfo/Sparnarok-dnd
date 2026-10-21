import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BestiaryView } from './BestiaryView';
import { INITIAL_CREATURES, CANONICAL_MASTERIES } from '../services/apiClient';
import i18n from '../i18n';

describe('BestiaryView Component Suite', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('es');
  });

  const mockCreate = vi.fn().mockResolvedValue(undefined);
  const mockDelete = vi.fn().mockResolvedValue(undefined);
  const mockAddAction = vi.fn().mockResolvedValue(undefined);
  const mockLearnMastery = vi.fn().mockResolvedValue(undefined);
  const mockCloseCreateModal = vi.fn();

  const renderComponent = (props = {}) => {
    return render(
      <BestiaryView
        creatures={INITIAL_CREATURES}
        allMasteries={CANONICAL_MASTERIES}
        isCreateModalOpen={false}
        onCloseCreateModal={mockCloseCreateModal}
        onCreateCreature={mockCreate}
        onDeleteCreature={mockDelete}
        onAddAction={mockAddAction}
        onLearnMastery={mockLearnMastery}
        {...props}
      />
    );
  };

  it('renders creature cards with names, AC, HP, CR and abilities', () => {
    renderComponent();

    // Check Bugbear Stalker card
    expect(screen.getByText('Bugbear Stalker')).toBeInTheDocument();
    expect(screen.getByText('Gorgon Vanguard')).toBeInTheDocument();
    expect(screen.getByText('Drow Duelist')).toBeInTheDocument();

    // Check stats are rendered
    expect(screen.getAllByText('CR 1').length).toBeGreaterThan(0);
    expect(screen.getByText('27')).toBeInTheDocument(); // Bugbear HP
    expect(screen.getAllByText('16').length).toBeGreaterThan(0); // Bugbear AC / ability
  });

  it('filters creatures interactively when typing in search input', async () => {
    renderComponent();

    const searchInput = screen.getByPlaceholderText('Buscar criatura por nombre...');
    await userEvent.type(searchInput, 'Gorgon');

    expect(screen.getByText('Gorgon Vanguard')).toBeInTheDocument();
    expect(screen.queryByText('Bugbear Stalker')).not.toBeInTheDocument();
    expect(screen.queryByText('Drow Duelist')).not.toBeInTheDocument();
  });

  it('filters creatures by Type dropdown', async () => {
    renderComponent();

    const typeSelect = screen.getByDisplayValue('Todos los Tipos');
    fireEvent.change(typeSelect, { target: { value: 'Monstrosity' } });

    expect(screen.getByText('Gorgon Vanguard')).toBeInTheDocument();
    expect(screen.queryByText('Bugbear Stalker')).not.toBeInTheDocument();
  });

  it('opens detailed stat block modal when clicking Ver Ficha Táctica', async () => {
    renderComponent();

    const inspectButtons = screen.getAllByRole('button', { name: /Ver Ficha Táctica/i });
    fireEvent.click(inspectButtons[0]);

    // Modal should be open showing detailed title and saving throw DC
    await waitFor(() => {
      expect(screen.getByText('Características y Modificadores')).toBeInTheDocument();
      expect(screen.getByText('Acciones y Ataques Tácticos')).toBeInTheDocument();
      expect(screen.getByText('Propiedades de Maestría de Armas (2024)')).toBeInTheDocument();
    });
  });

  it('submits new creature form when create modal is open', async () => {
    renderComponent({ isCreateModalOpen: true });

    expect(
      screen.getByRole('heading', { name: /Registrar Nueva Criatura en el Códice/i })
    ).toBeInTheDocument();

    const nameInput = screen.getByLabelText(/Nombre de la Criatura/i);
    await userEvent.type(nameInput, 'Ancient Red Dragon');

    const submitBtn = screen.getByRole('button', { name: /Crear Criatura/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(mockCreate).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'Ancient Red Dragon',
          size: 'Medium',
          type: 'Humanoid',
        })
      );
    });
  });
});
