import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  StashItemDto,
  CreateStashItemRequestDto,
  ReferenceItemSummaryDto,
  PartyFundDto,
  UpdatePartyFundsRequestDto,
  CraftingQueueItemDto,
  QueueCraftingItemRequestDto,
  StashActivityLogDto,
  DowntimeAdvanceResultDto,
  PartyStashDto,
} from '../types/stash';
import type { PartyResponseDto } from '../types/partyEncounter';
import { apiClient } from '../services/apiClient';
import { stashHubService } from '../services/stashHubService';

export interface PartyStashModalProps {
  isOpen: boolean;
  partyId?: string;
  partyName?: string;
  parties?: PartyResponseDto[];
  onPartySelect?: (partyId: string) => void;
  onClose: () => void;
  isEmbedded?: boolean;
}

type StashTab = 'stash' | 'crafting' | 'logs';

/**
 * Modal colaborativo para la gestión del inventario de la Party (Collaborative Party Stash).
 * Incluye:
 * 1. Sincronización en tiempo real vía WebSockets (SignalR).
 * 2. Gestión de monedas y Party Fund con recálculo automático.
 * 3. Módulo de Fabricación en descanso (Downtime Crafting) con avance de días por el DM.
 * 4. Historial de transacciones y auditoría (Activity Log).
 */
export function PartyStashModal({
  isOpen,
  partyId: propPartyId,
  partyName: propPartyName,
  parties = [],
  onPartySelect,
  onClose,
  isEmbedded = false,
}: PartyStashModalProps) {
  const { t } = useTranslation();

  const [selectedPartyId, setSelectedPartyId] = useState<string>(
    propPartyId || (parties && parties.length > 0 ? parties[0].id : '')
  );

  useEffect(() => {
    if (propPartyId) {
      setSelectedPartyId(propPartyId);
    } else if (parties && parties.length > 0 && !selectedPartyId) {
      setSelectedPartyId(parties[0].id);
    }
  }, [propPartyId, parties]);

  const activePartyId = selectedPartyId || propPartyId || '';
  const currentPartyObj = parties.find((p) => p.id === activePartyId);
  const activePartyName = currentPartyObj?.name || propPartyName || t('stash.defaultPartyName', 'Party');

  // Tab State
  const [activeTab, setActiveTab] = useState<StashTab>('stash');

  // Stash Items State
  const [items, setItems] = useState<StashItemDto[]>([]);
  const [totalWeight, setTotalWeight] = useState<number>(0);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [isAddFormOpen, setIsAddFormOpen] = useState<boolean>(false);

  // Funds State
  const [funds, setFunds] = useState<PartyFundDto>({
    partyId: activePartyId,
    copperPieces: 0,
    silverPieces: 0,
    electrumPieces: 0,
    goldPieces: 100,
    platinumPieces: 0,
    totalGoldEquivalent: 100,
  });
  const [isEditingFunds, setIsEditingFunds] = useState<boolean>(false);
  const [editCp, setEditCp] = useState<number>(0);
  const [editSp, setEditSp] = useState<number>(0);
  const [editEp, setEditEp] = useState<number>(0);
  const [editGp, setEditGp] = useState<number>(0);
  const [editPp, setEditPp] = useState<number>(0);

  // Crafting Queue State
  const [craftingQueue, setCraftingQueue] = useState<CraftingQueueItemDto[]>([]);
  const [isQueueFormOpen, setIsQueueFormOpen] = useState<boolean>(false);
  const [craftItemName, setCraftItemName] = useState<string>('');
  const [craftCost, setCraftCost] = useState<number>(50);
  const [craftDays, setCraftDays] = useState<number>(5);
  const [craftRarity, setCraftRarity] = useState<string>('Common');
  const [craftType, setCraftType] = useState<string>('Adventuring Gear');
  const [craftCrafter, setCraftCrafter] = useState<string>('');
  const [craftWeight, setCraftWeight] = useState<number>(1);
  const [craftDescription, setCraftDescription] = useState<string>('');
  const [craftIsSrd, setCraftIsSrd] = useState<boolean>(false);
  const [craftRefIndex, setCraftRefIndex] = useState<string | null>(null);
  const [craftingFeedback, setCraftingFeedback] = useState<string | null>(null);

  // Activity Logs State
  const [activityLogs, setActivityLogs] = useState<StashActivityLogDto[]>([]);

  // Item Form State (Stash)
  const [name, setName] = useState<string>('');
  const [quantity, setQuantity] = useState<number>(1);
  const [weight, setWeight] = useState<number>(0);
  const [description, setDescription] = useState<string>('');
  const [rarity, setRarity] = useState<string>('Common');
  const [itemType, setItemType] = useState<string>('Adventuring Gear');
  const [isOfficialSrd, setIsOfficialSrd] = useState<boolean>(false);
  const [referenceIndex, setReferenceIndex] = useState<string | null>(null);

  // Autocomplete State
  const [suggestions, setSuggestions] = useState<ReferenceItemSummaryDto[]>([]);
  const [isSearching, setIsSearching] = useState<boolean>(false);
  const [showSuggestions, setShowSuggestions] = useState<boolean>(false);
  const debounceTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Carga inicial y datos
  const loadInitialData = useCallback(async () => {
    if (!activePartyId) return;
    setIsLoading(true);
    try {
      const [stashData, logsData] = await Promise.all([
        apiClient.getPartyStash(activePartyId),
        apiClient.getStashActivityLogs(activePartyId, 40),
      ]);

      setItems(stashData.items);
      setTotalWeight(stashData.totalWeight);
      if (stashData.funds) {
        setFunds(stashData.funds);
        setEditCp(stashData.funds.copperPieces);
        setEditSp(stashData.funds.silverPieces);
        setEditEp(stashData.funds.electrumPieces);
        setEditGp(stashData.funds.goldPieces);
        setEditPp(stashData.funds.platinumPieces);
      }
      if (stashData.craftingQueue) {
        setCraftingQueue(stashData.craftingQueue);
      }
      setActivityLogs(logsData);
    } catch {
      // Ignorar fallback
    } finally {
      setIsLoading(false);
    }
  }, [activePartyId]);

  // Suscripción a eventos en tiempo real con SignalR
  useEffect(() => {
    if (!isOpen || !activePartyId) return;

    loadInitialData();

    // Iniciar conexión SignalR
    stashHubService.startConnection(activePartyId, {
      onItemAdded: (newItem: StashItemDto) => {
        setItems((prev) => {
          if (prev.some((i) => i.id === newItem.id)) {
            return prev.map((i) => (i.id === newItem.id ? newItem : i));
          }
          const next = [newItem, ...prev];
          setTotalWeight(next.reduce((acc, curr) => acc + curr.weight * curr.quantity, 0));
          return next;
        });
      },
      onItemDeleted: (deletedItemId: string) => {
        setItems((prev) => {
          const next = prev.filter((i) => i.id !== deletedItemId);
          setTotalWeight(next.reduce((acc, curr) => acc + curr.weight * curr.quantity, 0));
          return next;
        });
      },
      onFundsUpdated: (updatedFunds: PartyFundDto) => {
        setFunds(updatedFunds);
        setEditCp(updatedFunds.copperPieces);
        setEditSp(updatedFunds.silverPieces);
        setEditEp(updatedFunds.electrumPieces);
        setEditGp(updatedFunds.goldPieces);
        setEditPp(updatedFunds.platinumPieces);
      },
      onCraftingQueueUpdated: (updatedQueue: CraftingQueueItemDto[]) => {
        setCraftingQueue(updatedQueue);
      },
      onActivityLogged: (log: StashActivityLogDto) => {
        setActivityLogs((prev) => [log, ...prev.filter((l) => l.id !== log.id)]);
      },
      onDowntimeAdvanced: (result: DowntimeAdvanceResultDto) => {
        setCraftingQueue(result.updatedQueue);
        if (result.updatedStash && result.updatedStash.length > 0) {
          setItems(result.updatedStash);
          const newWeight = result.updatedStash.reduce(
            (acc, curr) => acc + curr.weight * curr.quantity,
            0
          );
          setTotalWeight(newWeight);
        }
        if (result.completedItemsCount > 0) {
          setCraftingFeedback(
            `✨ ¡${result.completedItemsCount} objeto(s) completado(s) y transferidos al Stash! (${result.completedItemNames.join(', ')})`
          );
        }
      },
      onStashUpdated: (stash: PartyStashDto) => {
        setItems(stash.items);
        setTotalWeight(stash.totalWeight);
        if (stash.funds) setFunds(stash.funds);
        if (stash.craftingQueue) setCraftingQueue(stash.craftingQueue);
      },
    });

    return () => {
      stashHubService.stopConnection();
    };
  }, [isOpen, activePartyId, loadInitialData]);

  const resetForm = () => {
    setName('');
    setQuantity(1);
    setWeight(0);
    setDescription('');
    setRarity('Common');
    setItemType('Adventuring Gear');
    setIsOfficialSrd(false);
    setReferenceIndex(null);
    setSuggestions([]);
    setShowSuggestions(false);
  };

  const resetCraftingForm = () => {
    setCraftItemName('');
    setCraftCost(50);
    setCraftDays(5);
    setCraftRarity('Common');
    setCraftType('Adventuring Gear');
    setCraftCrafter('');
    setCraftWeight(1);
    setCraftDescription('');
    setCraftIsSrd(false);
    setCraftRefIndex(null);
  };

  // Debounced search para Stash o Crafting
  const handleNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value;
    setName(value);
    setIsOfficialSrd(false);
    setReferenceIndex(null);

    if (debounceTimerRef.current) clearTimeout(debounceTimerRef.current);
    if (!value.trim()) {
      setSuggestions([]);
      setShowSuggestions(false);
      setIsSearching(false);
      return;
    }

    setIsSearching(true);
    debounceTimerRef.current = setTimeout(async () => {
      try {
        const results = await apiClient.searchReferenceEquipment(value.trim());
        setSuggestions(results);
        setShowSuggestions(results.length > 0);
      } catch {
        setSuggestions([]);
      } finally {
        setIsSearching(false);
      }
    }, 250);
  };

  const handleSelectSuggestion = async (item: ReferenceItemSummaryDto) => {
    setShowSuggestions(false);
    setName(item.name);
    setIsSearching(true);
    try {
      const detail = await apiClient.getReferenceEquipmentDetail(item.index);
      setName(detail.name);
      setWeight(detail.weight ?? 0);
      setDescription(detail.description ?? '');
      setRarity(detail.rarity || 'Common');
      setItemType(detail.itemType || 'Adventuring Gear');
      setIsOfficialSrd(true);
      setReferenceIndex(detail.index);
    } catch {
      setName(item.name);
      setIsOfficialSrd(true);
      setReferenceIndex(item.index);
    } finally {
      setIsSearching(false);
    }
  };

  // Guardar ítem en el stash
  const handleSaveItem = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;

    const requestData: CreateStashItemRequestDto = {
      name: name.trim(),
      quantity: Math.max(1, quantity),
      weight: Math.max(0, weight),
      description: description.trim(),
      rarity: rarity || 'Common',
      itemType: itemType || 'Adventuring Gear',
      isOfficialSrd,
      referenceIndex,
    };

    try {
      const created = await apiClient.addStashItem(activePartyId, requestData);
      setItems((prev) => {
        if (prev.some((i) => i.id === created.id)) return prev;
        const next = [created, ...prev];
        setTotalWeight(next.reduce((acc, curr) => acc + curr.weight * curr.quantity, 0));
        return next;
      });
      setIsAddFormOpen(false);
      resetForm();
    } catch {
      // Fallback
    }
  };

  // Eliminar ítem del stash
  const handleDeleteItem = async (itemId: string) => {
    try {
      await apiClient.deleteStashItem(activePartyId, itemId);
      setItems((prev) => {
        const next = prev.filter((i) => i.id !== itemId);
        setTotalWeight(next.reduce((acc, curr) => acc + curr.weight * curr.quantity, 0));
        return next;
      });
    } catch {
      // Fallback
    }
  };

  // Identificar objeto mágico (Acción exclusiva del DM)
  const handleIdentifyItem = async (itemId: string) => {
    try {
      const updated = await apiClient.identifyStashItem(activePartyId, itemId);
      setItems((prev) => prev.map((i) => (i.id === itemId ? updated : i)));
    } catch {
      // Fallback
    }
  };

  // Guardar actualización de monedas (Party Fund)
  const handleSaveFunds = async (e: React.FormEvent) => {
    e.preventDefault();
    const req: UpdatePartyFundsRequestDto = {
      copperPieces: Math.max(0, editCp),
      silverPieces: Math.max(0, editSp),
      electrumPieces: Math.max(0, editEp),
      goldPieces: Math.max(0, editGp),
      platinumPieces: Math.max(0, editPp),
    };

    try {
      const updated = await apiClient.updatePartyFunds(activePartyId, req);
      setFunds(updated);
      setIsEditingFunds(false);
    } catch {
      // Fallback
    }
  };

  // Encolar objeto en Crafting Queue
  const handleQueueCrafting = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!craftItemName.trim()) return;

    if (funds.goldPieces < craftCost) {
      alert(t('stash.insufficientGold', 'Fondos insuficientes en el Party Fund para iniciar este proyecto.'));
      return;
    }

    const req: QueueCraftingItemRequestDto = {
      itemName: craftItemName.trim(),
      itemType: craftType,
      rarity: craftRarity,
      goldCost: craftCost,
      daysRequired: Math.max(1, craftDays),
      weight: Math.max(0, craftWeight),
      description: craftDescription.trim(),
      isOfficialSrd: craftIsSrd,
      referenceIndex: craftRefIndex,
      crafterName: craftCrafter.trim() || undefined,
    };

    try {
      const queued = await apiClient.queueCraftingItem(activePartyId, req);
      setCraftingQueue((prev) => [queued, ...prev.filter((c) => c.id !== queued.id)]);
      setFunds((f) => ({
        ...f,
        goldPieces: Math.max(0, f.goldPieces - craftCost),
        totalGoldEquivalent: Math.max(0, f.totalGoldEquivalent - craftCost),
      }));
      setIsQueueFormOpen(false);
      resetCraftingForm();
    } catch {
      // Fallback
    }
  };

  // Avanzar 1 día de Downtime (DM exclusive action)
  const handleAdvanceDay = async () => {
    try {
      setCraftingFeedback(null);
      const result = await apiClient.advanceDowntimeDay(activePartyId);
      setCraftingQueue(result.updatedQueue);
      if (result.updatedStash && result.updatedStash.length > 0) {
        setItems(result.updatedStash);
        const newWeight = result.updatedStash.reduce(
          (acc, curr) => acc + curr.weight * curr.quantity,
          0
        );
        setTotalWeight(newWeight);
      }
      setCraftingFeedback(
        `👑 Downtime avanzado 1 día. Proyectos completados: ${result.completedItemsCount}. Restantes en curso: ${result.activeItemsCount}.`
      );
    } catch {
      // Fallback
    }
  };

  if (!isOpen) return null;

  const modalBodyContent = (
    <div
      className={`stash-modal-card ${isEmbedded ? 'embedded-stash-card' : 'modal-card'}`}
      id="party-stash-modal"
      onClick={(e) => e.stopPropagation()}
      role="dialog"
      aria-modal="true"
      aria-labelledby="party-stash-modal-title"
    >
      {/* Cabecera del Stash con Live Sync Badge */}
      <header className="modal-header stash-modal-header">
        <div className="stash-header-info">
          <h2 id="party-stash-modal-title" className="stash-modal-title">
            🎒 {t('stash.title', 'Alijo de la Party (Party Stash)')}
          </h2>
          {parties && parties.length > 1 ? (
            <div className="stash-party-picker">
              <select
                id="stash-party-select"
                className="form-select form-select-sm"
                value={activePartyId}
                onChange={(e) => {
                  setSelectedPartyId(e.target.value);
                  onPartySelect?.(e.target.value);
                }}
                title={t('stash.selectPartyLabel', 'Seleccionar Party')}
              >
                {parties.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
            </div>
          ) : (
            <span className="stash-party-pill">{activePartyName}</span>
          )}
          <span className="live-sync-indicator" id="signalr-live-indicator" title="Conexión en vivo activa">
            <span className="live-sync-dot"></span>
            {t('stash.liveSyncBadge', '⚡ En Vivo')}
          </span>
        </div>

        <div className="stash-weight-badge-container">
          <span className="stash-weight-label">{t('stash.totalWeightLabel', 'Peso Acumulado:')}</span>
          <span className="stash-weight-pill" id="stash-total-weight">
            ⚖️ {totalWeight.toFixed(1)} lbs
          </span>
        </div>

        {!isEmbedded && (
          <button
            type="button"
            className="modal-close-btn"
            id="close-party-stash-btn"
            onClick={onClose}
            aria-label={t('common.close', 'Cerrar')}
          >
            &times;
          </button>
        )}
      </header>

        {/* Barra de Pestañas / Tabs */}
        <nav className="stash-nav-tabs" role="tablist">
          <button
            type="button"
            className={`stash-tab-btn ${activeTab === 'stash' ? 'active' : ''}`}
            id="tab-stash-btn"
            role="tab"
            aria-selected={activeTab === 'stash'}
            onClick={() => setActiveTab('stash')}
          >
            {t('stash.tabStash', '🎒 Stash & Monedas')}
          </button>
          <button
            type="button"
            className={`stash-tab-btn ${activeTab === 'crafting' ? 'active' : ''}`}
            id="tab-crafting-btn"
            role="tab"
            aria-selected={activeTab === 'crafting'}
            onClick={() => setActiveTab('crafting')}
          >
            {t('stash.tabCrafting', '⚒️ Cola de Fabricación')}
            {craftingQueue.filter((c) => c.status === 'InProgress').length > 0 && (
              <span className="tab-counter-badge" id="crafting-active-count">
                {craftingQueue.filter((c) => c.status === 'InProgress').length}
              </span>
            )}
          </button>
          <button
            type="button"
            className={`stash-tab-btn ${activeTab === 'logs' ? 'active' : ''}`}
            id="tab-logs-btn"
            role="tab"
            aria-selected={activeTab === 'logs'}
            onClick={() => setActiveTab('logs')}
          >
            {t('stash.tabLogs', '📜 Historial / Auditoría')}
          </button>
        </nav>

        {/* -------------------- TAB 1: STASH & MONEDAS -------------------- */}
        {activeTab === 'stash' && (
          <div className="tab-pane fade-in" id="stash-tab-pane">
            {/* Widget del Tesoro Compartido (Party Fund) */}
            <section className="party-funds-bar" id="party-funds-section">
              <div className="funds-summary-row">
                <div className="funds-title-area">
                  <span className="funds-title">
                    {t('stash.fundsTitle', '💰 Tesoro del Grupo (Party Fund)')}
                  </span>
                  <span className="funds-total-gold" id="party-total-gold-equiv">
                    {t('stash.totalGoldEquiv', 'Equivalente:')} <strong>{funds.totalGoldEquivalent.toFixed(1)} PO</strong>
                  </span>
                </div>

                <button
                  type="button"
                  className="btn btn-secondary btn-sm"
                  id="toggle-edit-funds-btn"
                  onClick={() => setIsEditingFunds((prev) => !prev)}
                >
                  {isEditingFunds ? '✖️ Cancelar' : `🪙 ${t('stash.editFundsBtn', 'Modificar Monedas')}`}
                </button>
              </div>

              {!isEditingFunds ? (
                <div className="funds-chips-container" id="funds-chips-display">
                  <div className="party-fund-chip chip-cp" title="Piezas de Cobre (CP)">
                    <span className="coin-icon">🥉</span>
                    <span className="coin-amount" id="fund-cp-amount">{funds.copperPieces}</span>
                    <span className="coin-label">{t('stash.copperAbbr', 'PC')}</span>
                  </div>
                  <div className="party-fund-chip chip-sp" title="Piezas de Plata (SP)">
                    <span className="coin-icon">🥈</span>
                    <span className="coin-amount" id="fund-sp-amount">{funds.silverPieces}</span>
                    <span className="coin-label">{t('stash.silverAbbr', 'PP')}</span>
                  </div>
                  <div className="party-fund-chip chip-ep" title="Piezas de Electro (EP)">
                    <span className="coin-icon">🔷</span>
                    <span className="coin-amount" id="fund-ep-amount">{funds.electrumPieces}</span>
                    <span className="coin-label">{t('stash.electrumAbbr', 'PE')}</span>
                  </div>
                  <div className="party-fund-chip chip-gp" title="Piezas de Oro (GP)">
                    <span className="coin-icon">🪙</span>
                    <span className="coin-amount" id="fund-gp-amount">{funds.goldPieces}</span>
                    <span className="coin-label">{t('stash.goldAbbr', 'PO')}</span>
                  </div>
                  <div className="party-fund-chip chip-pp" title="Piezas de Platino (PP)">
                    <span className="coin-icon">💎</span>
                    <span className="coin-amount" id="fund-pp-amount">{funds.platinumPieces}</span>
                    <span className="coin-label">{t('stash.platinumAbbr', 'PL')}</span>
                  </div>
                </div>
              ) : (
                <form className="funds-edit-form fade-in" id="edit-party-funds-form" onSubmit={handleSaveFunds}>
                  <div className="funds-grid-inputs">
                    <div className="fund-input-group">
                      <label htmlFor="funds-cp">🥉 {t('stash.copperAbbr', 'PC')}</label>
                      <input
                        type="number"
                        id="funds-cp"
                        className="form-input form-input-sm"
                        min="0"
                        value={editCp}
                        onChange={(e) => setEditCp(Math.max(0, parseInt(e.target.value, 10) || 0))}
                      />
                    </div>
                    <div className="fund-input-group">
                      <label htmlFor="funds-sp">🥈 {t('stash.silverAbbr', 'PP')}</label>
                      <input
                        type="number"
                        id="funds-sp"
                        className="form-input form-input-sm"
                        min="0"
                        value={editSp}
                        onChange={(e) => setEditSp(Math.max(0, parseInt(e.target.value, 10) || 0))}
                      />
                    </div>
                    <div className="fund-input-group">
                      <label htmlFor="funds-ep">🔷 {t('stash.electrumAbbr', 'PE')}</label>
                      <input
                        type="number"
                        id="funds-ep"
                        className="form-input form-input-sm"
                        min="0"
                        value={editEp}
                        onChange={(e) => setEditEp(Math.max(0, parseInt(e.target.value, 10) || 0))}
                      />
                    </div>
                    <div className="fund-input-group">
                      <label htmlFor="funds-gp">🪙 {t('stash.goldAbbr', 'PO')}</label>
                      <input
                        type="number"
                        id="funds-gp"
                        className="form-input form-input-sm"
                        min="0"
                        value={editGp}
                        onChange={(e) => setEditGp(Math.max(0, parseInt(e.target.value, 10) || 0))}
                      />
                    </div>
                    <div className="fund-input-group">
                      <label htmlFor="funds-pp">💎 {t('stash.platinumAbbr', 'PL')}</label>
                      <input
                        type="number"
                        id="funds-pp"
                        className="form-input form-input-sm"
                        min="0"
                        value={editPp}
                        onChange={(e) => setEditPp(Math.max(0, parseInt(e.target.value, 10) || 0))}
                      />
                    </div>
                  </div>
                  <div className="funds-form-actions">
                    <button type="submit" className="btn btn-success btn-sm" id="save-funds-btn">
                      💾 {t('stash.saveFundsBtn', 'Guardar Monedas')}
                    </button>
                  </div>
                </form>
              )}
            </section>

            {/* Acciones del Stash */}
            <div className="stash-actions-bar">
              {!isAddFormOpen ? (
                <button
                  type="button"
                  className="btn btn-primary"
                  id="open-add-stash-item-btn"
                  onClick={() => setIsAddFormOpen(true)}
                >
                  ➕ {t('stash.addItemBtn', 'Añadir Objeto al Stash')}
                </button>
              ) : (
                <button
                  type="button"
                  className="btn btn-secondary"
                  id="cancel-add-stash-item-btn"
                  onClick={() => {
                    setIsAddFormOpen(false);
                    resetForm();
                  }}
                >
                  ✖️ {t('stash.closeFormBtn', 'Cancelar')}
                </button>
              )}
            </div>

            {/* Formulario de Adición con Autocompletado */}
            {isAddFormOpen && (
              <form
                className="add-stash-item-form fade-in"
                id="add-stash-item-modal"
                onSubmit={handleSaveItem}
              >
                <h3 className="add-item-form-title">
                  ✨ {t('stash.formTitle', 'Nuevo Objeto para el Alijo')}
                </h3>

                <div className="form-group autocomplete-group">
                  <label htmlFor="stash-item-name-input">
                    {t('stash.itemNameLabel', 'Nombre del Objeto (SRD Oficial o Homebrew)')}
                  </label>
                  <div className="input-with-loader">
                    <input
                      type="text"
                      id="stash-item-name-input"
                      className="form-input"
                      placeholder={t('stash.namePlaceholder', 'Ej. Bag of Holding, Dagger, Potion of Healing...')}
                      value={name}
                      onChange={handleNameChange}
                      autoComplete="off"
                      required
                    />
                    {isSearching && (
                      <span className="input-spinner" id="stash-search-loading" title="Buscando en compendio SRD...">
                        ⏳
                      </span>
                    )}
                  </div>

                  {isOfficialSrd && (
                    <div className="srd-verified-badge" id="stash-item-srd-badge">
                      🛡️ {t('stash.srdVerified', 'Objeto oficial importado de D&D 5e SRD')}
                    </div>
                  )}

                  {showSuggestions && suggestions.length > 0 && (
                    <ul className="autocomplete-suggestions-list" id="stash-item-suggestions" role="listbox">
                      {suggestions.map((item) => (
                        <li
                          key={item.index}
                          id={`suggestion-${item.index}`}
                          className="suggestion-item"
                          role="option"
                          onClick={() => handleSelectSuggestion(item)}
                        >
                          <span className="suggestion-name">{item.name}</span>
                          <span className="suggestion-slug">({item.index})</span>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>

                <div className="form-grid-row">
                  <div className="form-group">
                    <label htmlFor="stash-item-quantity-input">{t('stash.quantityLabel', 'Cantidad')}</label>
                    <input
                      type="number"
                      id="stash-item-quantity-input"
                      className="form-input"
                      min="1"
                      value={quantity}
                      onChange={(e) => setQuantity(Math.max(1, parseInt(e.target.value, 10) || 1))}
                      required
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="stash-item-weight-input">{t('stash.weightLabel', 'Peso Unitario (lbs)')}</label>
                    <input
                      type="number"
                      id="stash-item-weight-input"
                      className="form-input"
                      min="0"
                      step="0.1"
                      value={weight}
                      onChange={(e) => setWeight(Math.max(0, parseFloat(e.target.value) || 0))}
                      required
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="stash-item-rarity-select">{t('stash.rarityLabel', 'Rareza')}</label>
                    <select
                      id="stash-item-rarity-select"
                      className="form-select"
                      value={rarity}
                      onChange={(e) => setRarity(e.target.value)}
                    >
                      <option value="Common">Common (Común)</option>
                      <option value="Uncommon">Uncommon (Poco Común)</option>
                      <option value="Rare">Rare (Raro)</option>
                      <option value="Very Rare">Very Rare (Muy Raro)</option>
                      <option value="Legendary">Legendary (Legendario)</option>
                      <option value="Artifact">Artifact (Artefacto)</option>
                    </select>
                  </div>

                  <div className="form-group">
                    <label htmlFor="stash-item-type-input">{t('stash.typeLabel', 'Tipo / Categoría')}</label>
                    <input
                      type="text"
                      id="stash-item-type-input"
                      className="form-input"
                      placeholder="Ej. Wondrous Items, Weapons..."
                      value={itemType}
                      onChange={(e) => setItemType(e.target.value)}
                    />
                  </div>
                </div>

                <div className="form-group">
                  <label htmlFor="stash-item-description-input">{t('stash.descLabel', 'Descripción y Propiedades')}</label>
                  <textarea
                    id="stash-item-description-input"
                    className="form-textarea"
                    rows={3}
                    placeholder={t('stash.descPlaceholder', 'Notas mágicas, reglas, efectos o procedencia...')}
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                  />
                </div>

                <div className="form-actions-row">
                  <button
                    type="submit"
                    className="btn btn-success"
                    id="save-stash-item-btn"
                    disabled={!name.trim()}
                  >
                    💾 {t('stash.saveItemBtn', 'Guardar en el Stash')}
                  </button>
                </div>
              </form>
            )}

            {/* Listado de Objetos del Stash */}
            <div className="stash-items-container">
              {isLoading ? (
                <p className="stash-loading-msg">⏳ {t('common.loading', 'Cargando alijo...')}</p>
              ) : items.length === 0 ? (
                <div className="empty-stash-placeholder" id="empty-stash-placeholder">
                  <span className="empty-stash-icon">🎒</span>
                  <p>{t('stash.emptyMsg', 'El alijo de esta Party está vacío. ¡Añade objetos oficiales o tesoros homebrew!')}</p>
                </div>
              ) : (
                <div className="table-responsive">
                  <table className="stash-table" id="stash-items-table">
                    <thead>
                      <tr>
                        <th>{t('stash.colName', 'Objeto')}</th>
                        <th>{t('stash.colType', 'Categoría')}</th>
                        <th>{t('stash.colRarity', 'Rareza')}</th>
                        <th>{t('stash.colQty', 'Cant.')}</th>
                        <th>{t('stash.colUnitWeight', 'Peso Un.')}</th>
                        <th>{t('stash.colTotalWeight', 'Peso Total')}</th>
                        <th>{t('stash.colActions', 'Acciones')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {items.map((item) => (
                        <tr key={item.id} className="stash-item-row" id={`stash-item-row-${item.id}`}>
                          <td>
                            <div className="item-name-cell">
                              <strong className="item-title">{item.name}</strong>
                              {item.isOfficialSrd && (
                                <span className="badge-srd-tag" title="Objeto oficial SRD">SRD</span>
                              )}
                              {item.isIdentified === false && (
                                <span className="unidentified-badge" id={`unidentified-badge-${item.id}`} title="Objeto no identificado">
                                  ❓ {t('stash.unidentified', 'No Identificado')}
                                </span>
                              )}
                              {item.description && (
                                <p className="item-mini-desc">{item.description}</p>
                              )}
                            </div>
                          </td>
                          <td>{item.itemType}</td>
                          <td>
                            <span className={`rarity-pill rarity-${item.rarity.toLowerCase().replace(/\s+/g, '-')}`}>
                              {item.rarity}
                            </span>
                          </td>
                          <td><strong>x{item.quantity}</strong></td>
                          <td>{item.weight} lbs</td>
                          <td><strong>{(item.weight * item.quantity).toFixed(1)} lbs</strong></td>
                          <td className="actions-cell">
                            {item.isIdentified === false && (
                              <button
                                type="button"
                                className="btn-identify-icon"
                                id={`identify-stash-item-${item.id}`}
                                onClick={() => handleIdentifyItem(item.id)}
                                title={t('stash.identifyTooltip', 'Identificar Objeto (DM)')}
                              >
                                🔍 {t('stash.identifyBtn', 'Identificar')}
                              </button>
                            )}
                            <button
                              type="button"
                              className="btn-danger-icon"
                              id={`delete-stash-item-${item.id}`}
                              onClick={() => handleDeleteItem(item.id)}
                              aria-label={`Eliminar ${item.name}`}
                              title="Eliminar del Stash"
                            >
                              🗑️
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        )}

        {/* -------------------- TAB 2: COLA DE FABRICACIÓN (DOWNTIME CRAFTING) -------------------- */}
        {activeTab === 'crafting' && (
          <div className="tab-pane fade-in" id="crafting-tab-pane">
            {/* Feedback alert de Downtime */}
            {craftingFeedback && (
              <div className="crafting-feedback-banner fade-in" id="crafting-feedback-msg">
                {craftingFeedback}
              </div>
            )}

            {/* Barra de Acciones de Fabricación */}
            <div className="crafting-actions-bar">
              <div className="crafting-actions-left">
                {!isQueueFormOpen ? (
                  <button
                    type="button"
                    className="btn btn-primary"
                    id="open-queue-crafting-btn"
                    onClick={() => setIsQueueFormOpen(true)}
                  >
                    {t('stash.queueItemBtn', '⚒️ Encolar Fabricación')}
                  </button>
                ) : (
                  <button
                    type="button"
                    className="btn btn-secondary"
                    id="cancel-queue-crafting-btn"
                    onClick={() => {
                      setIsQueueFormOpen(false);
                      resetCraftingForm();
                    }}
                  >
                    ✖️ {t('stash.closeFormBtn', 'Cancelar')}
                  </button>
                )}
              </div>

              {/* Botón exclusivo para el DM */}
              <div className="crafting-actions-right">
                <button
                  type="button"
                  className="btn btn-dm-action"
                  id="advance-downtime-day-btn"
                  onClick={handleAdvanceDay}
                  title="Reduce 1 día el contador de todos los proyectos activos. Los completados pasan al alijo principal."
                >
                  {t('stash.advanceDowntimeBtn', '👑 Avanzar 1 día de Downtime (DM)')}
                </button>
              </div>
            </div>

            {/* Formulario de encolado de nuevo ítem */}
            {isQueueFormOpen && (
              <form
                className="add-crafting-form fade-in"
                id="queue-crafting-form"
                onSubmit={handleQueueCrafting}
              >
                <h3 className="add-item-form-title">
                  ⚒️ {t('stash.craftingFormTitle', 'Iniciar Fabricación en Downtime')}
                </h3>

                <div className="form-grid-row">
                  <div className="form-group flex-2">
                    <label htmlFor="craft-name-input">
                      {t('stash.itemNameLabel', 'Nombre del Objeto')}
                    </label>
                    <input
                      type="text"
                      id="craft-name-input"
                      className="form-input"
                      placeholder="Ej. Potion of Healing, Cloak of Elvenkind..."
                      value={craftItemName}
                      onChange={(e) => setCraftItemName(e.target.value)}
                      required
                    />
                  </div>

                  <div className="form-group flex-1">
                    <label htmlFor="craft-cost-input">
                      {t('stash.costLabel', 'Coste en Oro (PO)')}
                    </label>
                    <input
                      type="number"
                      id="craft-cost-input"
                      className="form-input"
                      min="0"
                      value={craftCost}
                      onChange={(e) => setCraftCost(Math.max(0, parseFloat(e.target.value) || 0))}
                      required
                    />
                  </div>

                  <div className="form-group flex-1">
                    <label htmlFor="craft-days-input">
                      {t('stash.daysLabel', 'Días Requeridos')}
                    </label>
                    <input
                      type="number"
                      id="craft-days-input"
                      className="form-input"
                      min="1"
                      value={craftDays}
                      onChange={(e) => setCraftDays(Math.max(1, parseInt(e.target.value, 10) || 1))}
                      required
                    />
                  </div>
                </div>

                <div className="form-grid-row">
                  <div className="form-group">
                    <label htmlFor="craft-rarity-select">{t('stash.rarityLabel', 'Rareza')}</label>
                    <select
                      id="craft-rarity-select"
                      className="form-select"
                      value={craftRarity}
                      onChange={(e) => setCraftRarity(e.target.value)}
                    >
                      <option value="Common">Common</option>
                      <option value="Uncommon">Uncommon</option>
                      <option value="Rare">Rare</option>
                      <option value="Very Rare">Very Rare</option>
                      <option value="Legendary">Legendary</option>
                    </select>
                  </div>

                  <div className="form-group">
                    <label htmlFor="craft-type-input">{t('stash.typeLabel', 'Categoría')}</label>
                    <input
                      type="text"
                      id="craft-type-input"
                      className="form-input"
                      placeholder="Ej. Potion, Scroll, Wondrous Item"
                      value={craftType}
                      onChange={(e) => setCraftType(e.target.value)}
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="craft-crafter-input">{t('stash.crafterLabel', 'Artesano Asignado')}</label>
                    <input
                      type="text"
                      id="craft-crafter-input"
                      className="form-input"
                      placeholder="Ej. Gimli, Gandalf..."
                      value={craftCrafter}
                      onChange={(e) => setCraftCrafter(e.target.value)}
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="craft-weight-input">{t('stash.weightLabel', 'Peso Unitario (lbs)')}</label>
                    <input
                      type="number"
                      id="craft-weight-input"
                      className="form-input"
                      min="0"
                      step="0.1"
                      value={craftWeight}
                      onChange={(e) => setCraftWeight(Math.max(0, parseFloat(e.target.value) || 0))}
                    />
                  </div>
                </div>

                <div className="form-group">
                  <label htmlFor="craft-description-input">{t('stash.descLabel', 'Descripción y Efectos')}</label>
                  <textarea
                    id="craft-description-input"
                    className="form-textarea"
                    rows={2}
                    placeholder={t('stash.descPlaceholder', 'Detalles mágicos o mecánicos...')}
                    value={craftDescription}
                    onChange={(e) => setCraftDescription(e.target.value)}
                  />
                </div>

                <div className="form-actions-row">
                  <span className="funds-balance-hint">
                    💰 Oro en tesoro: <strong>{funds.goldPieces} PO</strong> | Se descontarán: <strong>{craftCost} PO</strong>
                  </span>
                  <button
                    type="submit"
                    className="btn btn-success"
                    id="submit-queue-crafting-btn"
                    disabled={!craftItemName.trim()}
                  >
                    ⚒️ {t('stash.queueItemBtn', 'Encolar y Descontar Oro')}
                  </button>
                </div>
              </form>
            )}

            {/* Tabla de la Cola de Fabricación */}
            <div className="crafting-queue-container">
              {craftingQueue.length === 0 ? (
                <div className="empty-stash-placeholder" id="empty-crafting-placeholder">
                  <span className="empty-stash-icon">⚒️</span>
                  <p>{t('stash.emptyCrafting', 'No hay proyectos de fabricación en curso. ¡Encola consumibles o ítems mágicos durante el downtime!')}</p>
                </div>
              ) : (
                <div className="table-responsive">
                  <table className="stash-table" id="crafting-queue-table">
                    <thead>
                      <tr>
                        <th>{t('stash.colName', 'Objeto')}</th>
                        <th>{t('stash.colType', 'Categoría')}</th>
                        <th>{t('stash.colRarity', 'Rareza')}</th>
                        <th>{t('stash.costLabel', 'Coste (PO)')}</th>
                        <th>{t('stash.crafterLabel', 'Artesano')}</th>
                        <th>{t('stash.daysRemainingLabel', 'Progreso / Días')}</th>
                        <th>{t('stash.statusLabel', 'Estado')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {craftingQueue.map((item) => {
                        const progressPercent = Math.min(
                          100,
                          Math.max(0, Math.round(((item.daysRequired - item.daysRemaining) / item.daysRequired) * 100))
                        );
                        return (
                          <tr key={item.id} className="crafting-queue-row" id={`crafting-row-${item.id}`}>
                            <td>
                              <div className="item-name-cell">
                                <strong className="item-title">{item.itemName}</strong>
                                {item.description && (
                                  <p className="item-mini-desc">{item.description}</p>
                                )}
                              </div>
                            </td>
                            <td>{item.itemType}</td>
                            <td>
                              <span className={`rarity-pill rarity-${item.rarity.toLowerCase().replace(/\s+/g, '-')}`}>
                                {item.rarity}
                              </span>
                            </td>
                            <td><strong>{item.goldCost} PO</strong></td>
                            <td>{item.crafterName || <span className="text-muted">-</span>}</td>
                            <td>
                              <div className="crafting-progress-cell">
                                <div className="crafting-progress-bar-bg">
                                  <div
                                    className="crafting-progress-bar-fill"
                                    style={{ width: `${progressPercent}%` }}
                                  ></div>
                                </div>
                                <span className="crafting-days-pill">
                                  {item.status === 'Completed'
                                    ? '✅ Listo'
                                    : `⏳ ${item.daysRemaining} / ${item.daysRequired} días`}
                                </span>
                              </div>
                            </td>
                            <td>
                              <span
                                className={`status-badge ${
                                  item.status === 'Completed' ? 'status-badge-completed' : 'status-badge-progress'
                                }`}
                              >
                                {item.status === 'Completed'
                                  ? t('stash.statusCompleted', 'Completado')
                                  : t('stash.statusInProgress', 'En Progreso')}
                              </span>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        )}

        {/* -------------------- TAB 3: HISTORIAL / AUDIT LOG -------------------- */}
        {activeTab === 'logs' && (
          <div className="tab-pane fade-in" id="logs-tab-pane">
            <div className="activity-logs-container" id="stash-activity-logs-list">
              {activityLogs.length === 0 ? (
                <div className="empty-stash-placeholder" id="empty-logs-placeholder">
                  <span className="empty-stash-icon">📜</span>
                  <p>{t('stash.emptyLogs', 'No hay registros de actividad o transacciones aún para esta Party.')}</p>
                </div>
              ) : (
                <div className="activity-timeline">
                  {activityLogs.map((log) => (
                    <article key={log.id} className="activity-log-item" id={`activity-log-${log.id}`}>
                      <div className="activity-log-header">
                        <span className="activity-user-badge">{log.userDisplayName}</span>
                        <span className={`activity-action-tag action-${log.actionType.toLowerCase()}`}>
                          {log.actionType}
                        </span>
                        <time className="activity-log-time">
                          {new Date(log.timestampUtc).toLocaleDateString()} {new Date(log.timestampUtc).toLocaleTimeString()}
                        </time>
                      </div>
                      <p className="activity-log-desc">{log.description}</p>
                    </article>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}
      </div>
  );

  if (isEmbedded) {
    return <div className="embedded-stash-wrapper">{modalBodyContent}</div>;
  }

  return (
    <div className="modal-backdrop" id="party-stash-modal-backdrop" onClick={onClose}>
      {modalBodyContent}
    </div>
  );
}
