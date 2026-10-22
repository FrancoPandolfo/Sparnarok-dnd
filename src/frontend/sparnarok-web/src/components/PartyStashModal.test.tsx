import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PartyStashModal } from './PartyStashModal';
import { apiClient } from '../services/apiClient';
import { stashHubService } from '../services/stashHubService';
import i18n from '../i18n';
import type {
  PartyFundDto,
  CraftingQueueItemDto,
  StashActivityLogDto,
  DowntimeAdvanceResultDto,
} from '../types/stash';

vi.mock('../services/apiClient', () => ({
  apiClient: {
    getPartyStash: vi.fn(),
    searchReferenceEquipment: vi.fn(),
    getReferenceEquipmentDetail: vi.fn(),
    addStashItem: vi.fn(),
    deleteStashItem: vi.fn(),
    getPartyFunds: vi.fn(),
    updatePartyFunds: vi.fn(),
    getCraftingQueue: vi.fn(),
    queueCraftingItem: vi.fn(),
    advanceDowntimeDay: vi.fn(),
    getStashActivityLogs: vi.fn(),
  },
}));

vi.mock('../services/stashHubService', () => ({
  stashHubService: {
    startConnection: vi.fn().mockResolvedValue(undefined),
    stopConnection: vi.fn().mockResolvedValue(undefined),
  },
}));

describe('PartyStashModal Component Suite with QoL Features', () => {
  const mockPartyId = 'test-party-123';
  const mockPartyName = 'Fellowship of the Ring';

  const defaultFunds: PartyFundDto = {
    partyId: mockPartyId,
    copperPieces: 50,
    silverPieces: 20,
    electrumPieces: 0,
    goldPieces: 150,
    platinumPieces: 2,
    totalGoldEquivalent: 172.5,
  };

  const defaultCraftingQueue: CraftingQueueItemDto[] = [
    {
      id: 'craft-1',
      partyId: mockPartyId,
      itemName: 'Potion of Greater Healing',
      itemType: 'Potion',
      rarity: 'Uncommon',
      goldCost: 50,
      daysRequired: 3,
      daysRemaining: 2,
      weight: 0.5,
      description: 'Restores 4d4+4 HP',
      isOfficialSrd: true,
      referenceIndex: 'potion-of-greater-healing',
      crafterName: 'Gandalf',
      status: 'InProgress',
      createdAtUtc: new Date().toISOString(),
    },
  ];

  const defaultLogs: StashActivityLogDto[] = [
    {
      id: 'log-1',
      partyId: mockPartyId,
      userId: 'user-1',
      userDisplayName: 'Frodo',
      actionType: 'ItemAdded',
      description: 'Añadido 1x Elven Cloak al alijo.',
      timestampUtc: new Date().toISOString(),
    },
    {
      id: 'log-2',
      partyId: mockPartyId,
      userId: 'user-dm',
      userDisplayName: 'Dungeon Master',
      actionType: 'DowntimeAdvanced',
      description: 'Avanzado 1 día de Downtime.',
      timestampUtc: new Date().toISOString(),
    },
  ];

  beforeEach(async () => {
    vi.clearAllMocks();
    await i18n.changeLanguage('es');

    vi.mocked(apiClient.getPartyStash).mockResolvedValue({
      partyId: mockPartyId,
      partyName: mockPartyName,
      totalWeight: 0,
      items: [],
      funds: defaultFunds,
      craftingQueue: defaultCraftingQueue,
    });

    vi.mocked(apiClient.getStashActivityLogs).mockResolvedValue(defaultLogs);
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

  it('initializes SignalR real-time connection on open and cleans up on unmount', async () => {
    const { unmount } = render(
      <PartyStashModal
        isOpen={true}
        partyId={mockPartyId}
        partyName={mockPartyName}
        onClose={vi.fn()}
      />
    );

    expect(stashHubService.startConnection).toHaveBeenCalledWith(
      mockPartyId,
      expect.objectContaining({
        onItemAdded: expect.any(Function),
        onItemDeleted: expect.any(Function),
        onFundsUpdated: expect.any(Function),
        onCraftingQueueUpdated: expect.any(Function),
        onActivityLogged: expect.any(Function),
        onDowntimeAdvanced: expect.any(Function),
      })
    );

    unmount();
    expect(stashHubService.stopConnection).toHaveBeenCalled();
  });

  it('renders party fund currencies and allows modifying coins', async () => {
    const user = userEvent.setup();

    render(
      <PartyStashModal
        isOpen={true}
        partyId={mockPartyId}
        partyName={mockPartyName}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('150')).toBeDefined(); // GP
      expect(document.getElementById('party-total-gold-equiv')?.textContent).toContain('172.5');
    });

    // Click "Modificar Monedas"
    const editFundsBtn = screen.getByRole('button', { name: /Modificar Monedas/i });
    await user.click(editFundsBtn);

    // Edit GP input
    const gpInput = document.getElementById('funds-gp') as HTMLInputElement;
    fireEvent.change(gpInput, { target: { value: '200' } });

    const updatedFunds: PartyFundDto = {
      ...defaultFunds,
      goldPieces: 200,
      totalGoldEquivalent: 222.5,
    };
    vi.mocked(apiClient.updatePartyFunds).mockResolvedValueOnce(updatedFunds);

    const saveFundsBtn = screen.getByRole('button', { name: /Guardar Monedas/i });
    await user.click(saveFundsBtn);

    await waitFor(() => {
      expect(apiClient.updatePartyFunds).toHaveBeenCalledWith(mockPartyId, {
        copperPieces: 50,
        silverPieces: 20,
        electrumPieces: 0,
        goldPieces: 200,
        platinumPieces: 2,
      });
      expect(document.getElementById('party-total-gold-equiv')?.textContent).toContain('222.5');
    });
  });

  it('switches to Crafting Queue tab, renders active queue and queues a new crafting project', async () => {
    const user = userEvent.setup();

    render(
      <PartyStashModal
        isOpen={true}
        partyId={mockPartyId}
        partyName={mockPartyName}
        onClose={vi.fn()}
      />
    );

    // Switch to Crafting tab
    const craftingTabBtn = document.getElementById('tab-crafting-btn')!;
    await user.click(craftingTabBtn);

    await waitFor(() => {
      expect(screen.getByText('Potion of Greater Healing')).toBeDefined();
      expect(screen.getByText(/Gandalf/)).toBeDefined();
    });

    // Open Queue Crafting form
    const openQueueBtn = document.getElementById('open-queue-crafting-btn')!;
    await user.click(openQueueBtn);

    const nameInput = document.getElementById('craft-name-input')!;
    await user.type(nameInput, 'Scroll of Fireball');

    const costInput = document.getElementById('craft-cost-input')!;
    fireEvent.change(costInput, { target: { value: '100' } });

    const newQueuedItem: CraftingQueueItemDto = {
      id: 'craft-2',
      partyId: mockPartyId,
      itemName: 'Scroll of Fireball',
      itemType: 'Scroll',
      rarity: 'Uncommon',
      goldCost: 100,
      daysRequired: 5,
      daysRemaining: 5,
      weight: 0.1,
      description: 'Cast 8d6 Fireball',
      isOfficialSrd: false,
      crafterName: 'Wizard',
      status: 'InProgress',
      createdAtUtc: new Date().toISOString(),
    };
    vi.mocked(apiClient.queueCraftingItem).mockResolvedValueOnce(newQueuedItem);

    const submitQueueBtn = document.getElementById('submit-queue-crafting-btn')!;
    await user.click(submitQueueBtn);

    await waitFor(() => {
      expect(apiClient.queueCraftingItem).toHaveBeenCalledWith(mockPartyId, expect.objectContaining({
        itemName: 'Scroll of Fireball',
        goldCost: 100,
      }));
      expect(screen.getByText('Scroll of Fireball')).toBeDefined();
    });
  });

  it('handles DM Advance Downtime Day action and updates crafting progress', async () => {
    const user = userEvent.setup();

    render(
      <PartyStashModal
        isOpen={true}
        partyId={mockPartyId}
        partyName={mockPartyName}
        onClose={vi.fn()}
      />
    );

    // Switch to Crafting tab
    const craftingTabBtn = document.getElementById('tab-crafting-btn')!;
    await user.click(craftingTabBtn);

    const advanceResult: DowntimeAdvanceResultDto = {
      daysAdvanced: 1,
      completedItemsCount: 1,
      activeItemsCount: 0,
      completedItemNames: ['Potion of Greater Healing'],
      updatedQueue: [
        {
          ...defaultCraftingQueue[0],
          daysRemaining: 0,
          status: 'Completed',
        },
      ],
      updatedStash: [
        {
          id: 'stash-potion',
          partyId: mockPartyId,
          name: 'Potion of Greater Healing',
          quantity: 1,
          weight: 0.5,
          description: 'Restores 4d4+4 HP',
          rarity: 'Uncommon',
          itemType: 'Potion',
          isOfficialSrd: true,
          referenceIndex: 'potion-of-greater-healing',
          createdAt: new Date().toISOString(),
        },
      ],
    };
    vi.mocked(apiClient.advanceDowntimeDay).mockResolvedValueOnce(advanceResult);

    // Click DM Advance Day button
    const advanceBtn = document.getElementById('advance-downtime-day-btn')!;
    await user.click(advanceBtn);

    await waitFor(() => {
      expect(apiClient.advanceDowntimeDay).toHaveBeenCalledWith(mockPartyId);
      expect(document.getElementById('crafting-feedback-msg')?.textContent).toContain('Downtime avanzado 1 día');
    });
  });

  it('switches to Activity Log tab and displays chronological transaction history', async () => {
    const user = userEvent.setup();

    render(
      <PartyStashModal
        isOpen={true}
        partyId={mockPartyId}
        partyName={mockPartyName}
        onClose={vi.fn()}
      />
    );

    // Switch to Logs tab
    const logsTabBtn = document.getElementById('tab-logs-btn')!;
    await user.click(logsTabBtn);

    await waitFor(() => {
      expect(screen.getByText('Frodo')).toBeDefined();
      expect(screen.getByText(/Añadido 1x Elven Cloak al alijo/)).toBeDefined();
      expect(screen.getByText('Dungeon Master')).toBeDefined();
      expect(screen.getByText(/Avanzado 1 día de Downtime/)).toBeDefined();
    });
  });
});
