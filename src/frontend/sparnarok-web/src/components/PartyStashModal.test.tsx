import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PartyStashModal } from './PartyStashModal';
import { apiClient } from '../services/apiClient';
import i18n from '../i18n';
import type { PartyStashDto, ReferenceItemDetailDto, ReferenceItemSummaryDto, StashItemDto } from '../types/stash';

vi.mock('../services/apiClient', () => ({
  apiClient: {
    getPartyStash: vi.fn(),
    searchReferenceEquipment: vi.fn(),
    getReferenceEquipmentDetail: vi.fn(),
    addStashItem: vi.fn(),
    deleteStashItem: vi.fn(),
  },
}));

describe('PartyStashModal Component Suite', () => {
  const mockPartyId = 'test-party-123';
  const mockPartyName = 'Fellowship of the Ring';

  beforeEach(async () => {
    vi.clearAllMocks();
    await i18n.changeLanguage('es');
  });

  it('does not render when isOpen is false', () => {
    const { container } = render(
      <PartyStashModal
        isOpen={false}
        partyId={mockPartyId}
        partyName={mockPartyName}
        onClose={vi.fn()}
      />
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders party stash modal with empty items list and accumulated weight', async () => {
    vi.mocked(apiClient.getPartyStash).mockResolvedValueOnce({
      partyId: mockPartyId,
      totalWeight: 0,
      items: [],
    } as PartyStashDto);

    render(
      <PartyStashModal
        isOpen={true}
        partyId={mockPartyId}
        partyName={mockPartyName}
        onClose={vi.fn()}
      />
    );

    expect(screen.getByText(mockPartyName)).toBeDefined();
    expect(screen.getByText(/0\.0 lbs/)).toBeDefined();
    await waitFor(() => {
      expect(screen.getByText(/El alijo de esta Party está vacío/i)).toBeDefined();
    });
  });

  it('renders existing items and calculates total weight correctly', async () => {
    const mockItems: StashItemDto[] = [
      {
        id: 'item-1',
        partyId: mockPartyId,
        name: 'Rope of Climbing',
        quantity: 1,
        weight: 3,
        description: 'A 60-foot length of silk rope.',
        rarity: 'Uncommon',
        itemType: 'Wondrous Items',
        isOfficialSrd: true,
        referenceIndex: 'rope-of-climbing',
        createdAtUtc: new Date().toISOString(),
      },
      {
        id: 'item-2',
        partyId: mockPartyId,
        name: 'Healing Potion',
        quantity: 4,
        weight: 0.5,
        description: 'Restores 2d4+2 HP.',
        rarity: 'Common',
        itemType: 'Potions',
        isOfficialSrd: false,
        referenceIndex: null,
        createdAtUtc: new Date().toISOString(),
      },
    ];

    vi.mocked(apiClient.getPartyStash).mockResolvedValueOnce({
      partyId: mockPartyId,
      totalWeight: 5.0,
      items: mockItems,
    } as PartyStashDto);

    render(
      <PartyStashModal
        isOpen={true}
        partyId={mockPartyId}
        partyName={mockPartyName}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('Rope of Climbing')).toBeDefined();
      expect(screen.getByText('Healing Potion')).toBeDefined();
      expect(screen.getByText(/5\.0 lbs/)).toBeDefined();
    });
  });

  it('searches D&D 5e API with debounce, autofills on suggestion click, and supports manual edits', async () => {
    const user = userEvent.setup();
    vi.mocked(apiClient.getPartyStash).mockResolvedValueOnce({
      partyId: mockPartyId,
      totalWeight: 0,
      items: [],
    } as PartyStashDto);

    const mockSearchResults: ReferenceItemSummaryDto[] = [
      { index: 'dagger', name: 'Dagger', url: '/api/2024/equipment/dagger' },
    ];
    vi.mocked(apiClient.searchReferenceEquipment).mockResolvedValue(mockSearchResults);

    const mockDetail: ReferenceItemDetailDto = {
      index: 'dagger',
      name: 'Dagger',
      weight: 1,
      description: 'Finesse, light, thrown (range 20/60). Simple Melee Weapon.',
      rarity: 'Common',
      itemType: 'Weapons',
    };
    vi.mocked(apiClient.getReferenceEquipmentDetail).mockResolvedValue(mockDetail);

    const mockCreatedItem: StashItemDto = {
      id: 'item-dagger',
      partyId: mockPartyId,
      name: 'Dagger',
      quantity: 2,
      weight: 1,
      description: 'Finesse, light, thrown (range 20/60). Simple Melee Weapon.',
      rarity: 'Uncommon',
      itemType: 'Weapons',
      isOfficialSrd: true,
      referenceIndex: 'dagger',
      createdAtUtc: new Date().toISOString(),
    };
    vi.mocked(apiClient.addStashItem).mockResolvedValue(mockCreatedItem);

    render(
      <PartyStashModal
        isOpen={true}
        partyId={mockPartyId}
        partyName={mockPartyName}
        onClose={vi.fn()}
      />
    );

    // Open add item form
    const openAddBtn = screen.getByRole('button', { name: /Añadir Objeto al Stash/i });
    await user.click(openAddBtn);

    const nameInput = screen.getByRole('textbox', { name: /Nombre del Objeto/i });
    await user.type(nameInput, 'Dag');

    // Wait for debounced search call
    await waitFor(() => {
      expect(apiClient.searchReferenceEquipment).toHaveBeenCalledWith('Dag');
    }, { timeout: 1500 });

    // Suggestion should appear
    const suggestion = await screen.findByText('Dagger');
    expect(suggestion).toBeDefined();

    // Click suggestion
    await user.click(suggestion);

    // Detail should autofill
    await waitFor(() => {
      expect(apiClient.getReferenceEquipmentDetail).toHaveBeenCalledWith('dagger');
      const weightInput = screen.getByLabelText(/Peso Unitario/i) as HTMLInputElement;
      expect(weightInput.value).toBe('1');
      expect(screen.getByText(/Objeto oficial importado de D&D 5e SRD/i)).toBeDefined();
    });

    // User modifies quantity and rarity
    const qtyInput = screen.getByLabelText(/Cantidad/i);
    fireEvent.change(qtyInput, { target: { value: '2' } });

    const raritySelect = screen.getByLabelText(/Rareza/i);
    await user.selectOptions(raritySelect, 'Uncommon');

    // Submit form
    const saveBtn = screen.getByRole('button', { name: /Guardar en el Stash/i });
    await user.click(saveBtn);

    await waitFor(() => {
      expect(apiClient.addStashItem).toHaveBeenCalledWith(mockPartyId, expect.objectContaining({
        name: 'Dagger',
        quantity: 2,
        weight: 1,
        rarity: 'Uncommon',
        itemType: 'Weapons',
        isOfficialSrd: true,
        referenceIndex: 'dagger',
      }));
      expect(screen.getByText('Dagger')).toBeDefined();
    });
  });

  it('deletes an item and updates local state and total weight', async () => {
    const user = userEvent.setup();
    const mockItems: StashItemDto[] = [
      {
        id: 'item-del-1',
        partyId: mockPartyId,
        name: 'Heavy Greatsword',
        quantity: 1,
        weight: 6,
        description: 'Two-handed heavy weapon',
        rarity: 'Common',
        itemType: 'Weapons',
        isOfficialSrd: false,
        referenceIndex: null,
        createdAtUtc: new Date().toISOString(),
      },
    ];

    vi.mocked(apiClient.getPartyStash).mockResolvedValueOnce({
      partyId: mockPartyId,
      totalWeight: 6.0,
      items: mockItems,
    } as PartyStashDto);

    vi.mocked(apiClient.deleteStashItem).mockResolvedValueOnce();

    render(
      <PartyStashModal
        isOpen={true}
        partyId={mockPartyId}
        partyName={mockPartyName}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('Heavy Greatsword')).toBeDefined();
      expect(document.getElementById('stash-total-weight')?.textContent).toContain('6.0 lbs');
    });

    const deleteBtn = screen.getByRole('button', { name: /Eliminar Heavy Greatsword/i });
    await user.click(deleteBtn);

    await waitFor(() => {
      expect(apiClient.deleteStashItem).toHaveBeenCalledWith(mockPartyId, 'item-del-1');
      expect(screen.queryByText('Heavy Greatsword')).toBeNull();
      expect(document.getElementById('stash-total-weight')?.textContent).toContain('0.0 lbs');
    });
  });
});
