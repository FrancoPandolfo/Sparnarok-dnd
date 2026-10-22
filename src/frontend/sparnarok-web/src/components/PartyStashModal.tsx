import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  StashItemDto,
  CreateStashItemRequestDto,
  ReferenceItemSummaryDto,
} from '../types/stash';
import { apiClient } from '../services/apiClient';

interface PartyStashModalProps {
  isOpen: boolean;
  partyId: string;
  partyName: string;
  onClose: () => void;
}

/**
 * Modal para la gestión colaborativa del inventario de la Party (Collaborative Party Stash).
 * Incluye visualización de peso total, listado de objetos, y formulario de adición
 * con componente de autocompletado (Typeahead con debounce) conectado a D&D 5e API (SRD).
 */
export function PartyStashModal({
  isOpen,
  partyId,
  partyName,
  onClose,
}: PartyStashModalProps) {
  const { t } = useTranslation();

  const [items, setItems] = useState<StashItemDto[]>([]);
  const [totalWeight, setTotalWeight] = useState<number>(0);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [isAddFormOpen, setIsAddFormOpen] = useState<boolean>(false);

  // Form State
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

  // Carga inicial del stash
  const loadStash = useCallback(async () => {
    if (!partyId) return;
    setIsLoading(true);
    try {
      const data = await apiClient.getPartyStash(partyId);
      setItems(data.items);
      setTotalWeight(data.totalWeight);
    } catch {
      // Ignorar error en fallback
    } finally {
      setIsLoading(false);
    }
  }, [partyId]);

  useEffect(() => {
    if (isOpen && partyId) {
      loadStash();
      setIsAddFormOpen(false);
      resetForm();
    }
  }, [isOpen, partyId, loadStash]);

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

  // Debounced search al escribir en el nombre
  const handleNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value;
    setName(value);
    setIsOfficialSrd(false);
    setReferenceIndex(null);

    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
    }

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

  // Seleccionar un ítem oficial de la lista de sugerencias
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
      const created = await apiClient.addStashItem(partyId, requestData);
      setItems((prev) => [created, ...prev]);
      setTotalWeight((prev) => prev + created.weight * created.quantity);
      setIsAddFormOpen(false);
      resetForm();
    } catch {
      // Fallback
    }
  };

  // Eliminar ítem del stash
  const handleDeleteItem = async (itemId: string) => {
    const itemToDelete = items.find((i) => i.id === itemId);
    try {
      await apiClient.deleteStashItem(partyId, itemId);
      setItems((prev) => prev.filter((i) => i.id !== itemId));
      if (itemToDelete) {
        setTotalWeight((prev) => Math.max(0, prev - itemToDelete.weight * itemToDelete.quantity));
      }
    } catch {
      // Fallback
    }
  };

  if (!isOpen) return null;

  return (
    <div className="modal-backdrop" id="party-stash-modal-backdrop" onClick={onClose}>
      <div
        className="modal-card stash-modal-card"
        id="party-stash-modal"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="party-stash-modal-title"
      >
        {/* Cabecera del Stash */}
        <header className="modal-header stash-modal-header">
          <div className="stash-header-info">
            <h2 id="party-stash-modal-title" className="stash-modal-title">
              🎒 {t('stash.title', 'Alijo de la Party (Party Stash)')}
            </h2>
            <span className="stash-party-pill">{partyName}</span>
          </div>
          <div className="stash-weight-badge-container">
            <span className="stash-weight-label">{t('stash.totalWeightLabel', 'Peso Acumulado:')}</span>
            <span className="stash-weight-pill" id="stash-total-weight">
              ⚖️ {totalWeight.toFixed(1)} lbs
            </span>
          </div>
          <button
            type="button"
            className="modal-close-btn"
            id="close-party-stash-btn"
            onClick={onClose}
            aria-label={t('common.close', 'Cerrar')}
          >
            &times;
          </button>
        </header>

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

            {/* Fila: Nombre con Autocomplete */}
            <div className="form-group autocomplete-group">
              <label htmlFor="stash-item-name-input">
                {t('stash.itemNameLabel', 'Nombre del Objeto (SRD Oficial o Homebrew)')}
              </label>
              <div className="input-with-loader">
                <input
                  type="text"
                  id="stash-item-name-input"
                  className="form-input"
                  placeholder={t('stash.namePlaceholder', 'Ej. Bag of Holding, Dagger, Poción...')}
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

              {/* Badge indicando si es SRD Oficial */}
              {isOfficialSrd && (
                <div className="srd-verified-badge" id="stash-item-srd-badge">
                  🛡️ {t('stash.srdVerified', 'Objeto oficial importado de D&D 5e SRD')}
                </div>
              )}

              {/* Menú de sugerencias desplegable */}
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

            {/* Fila de Propiedades Tácticas (Peso, Cantidad, Rareza, Tipo) */}
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

            {/* Fila: Descripción */}
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
                      <td>
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
    </div>
  );
}
