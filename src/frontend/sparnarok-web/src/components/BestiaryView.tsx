import React, { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  CreatureResponseDto,
  CreatureSize,
  CreatureType,
  WeaponMasteryResponseDto,
  CreateCreatureRequestDto,
  CreateActionRequestDto,
} from '../types/dnd';
import { CreatureCard } from './CreatureCard';
import { CreatureDetailModal } from './CreatureDetailModal';
import { CreatureFormModal } from './CreatureFormModal';
import { ActionFormModal } from './ActionFormModal';

export interface BestiaryViewProps {
  creatures: CreatureResponseDto[];
  allMasteries: WeaponMasteryResponseDto[];
  isCreateModalOpen: boolean;
  onCloseCreateModal: () => void;
  onCreateCreature: (data: CreateCreatureRequestDto) => Promise<void>;
  onDeleteCreature: (id: string, name: string) => Promise<void>;
  onAddAction: (creatureId: string, data: CreateActionRequestDto) => Promise<void>;
  onLearnMastery: (creatureId: string, masteryId: string) => Promise<void>;
}

const CREATURE_SIZES: CreatureSize[] = ['Tiny', 'Small', 'Medium', 'Large', 'Huge', 'Gargantuan'];

const CREATURE_TYPES: CreatureType[] = [
  'Aberration',
  'Beast',
  'Celestial',
  'Construct',
  'Dragon',
  'Elemental',
  'Fey',
  'Fiend',
  'Giant',
  'Humanoid',
  'Monstrosity',
  'Ooze',
  'Plant',
  'Undead',
];

/**
 * Vista principal del Bestiario: listado, filtrado multidimensional y modales tácticos.
 */
export const BestiaryView: React.FC<BestiaryViewProps> = ({
  creatures,
  allMasteries,
  isCreateModalOpen,
  onCloseCreateModal,
  onCreateCreature,
  onDeleteCreature,
  onAddAction,
  onLearnMastery,
}) => {
  const { t } = useTranslation();

  const [searchTerm, setSearchTerm] = useState('');
  const [selectedSize, setSelectedSize] = useState<string>('');
  const [selectedType, setSelectedType] = useState<string>('');

  const [selectedCreature, setSelectedCreature] = useState<CreatureResponseDto | null>(null);
  const [actionModalCreature, setActionModalCreature] = useState<CreatureResponseDto | null>(null);

  // Normalizador de texto tolerante a tildes y mayúsculas
  const normalize = (val: string | undefined | null) =>
    (val || '')
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .trim();

  // Filtrado reactivo en memoria multi-criterio (Nombre, Tipo, Alineamiento, Acciones)
  const filteredCreatures = useMemo(() => {
    const term = normalize(searchTerm);
    const targetSize = normalize(selectedSize);
    const targetType = normalize(selectedType);

    return creatures.filter((c) => {
      const nameMatch = normalize(c.name).includes(term);
      const typeTextMatch = normalize(c.type).includes(term);
      const alignmentMatch = normalize(c.alignment).includes(term);
      const actionMatch = c.actions?.some((a) => normalize(a.name).includes(term));
      const matchesSearch = !term || nameMatch || typeTextMatch || alignmentMatch || actionMatch;

      const matchesSize = !targetSize || normalize(c.size) === targetSize;
      const matchesType = !targetType || normalize(c.type) === targetType;

      return matchesSearch && matchesSize && matchesType;
    });
  }, [creatures, searchTerm, selectedSize, selectedType]);

  const handleClearFilters = () => {
    setSearchTerm('');
    setSelectedSize('');
    setSelectedType('');
  };

  const handleDeleteWithConfirm = async (id: string, name: string) => {
    if (window.confirm(t('bestiary.confirmDelete', { name }))) {
      await onDeleteCreature(id, name);
      if (selectedCreature?.id === id) {
        setSelectedCreature(null);
      }
    }
  };

  const handleOpenAddActionFromDetail = (creature: CreatureResponseDto) => {
    setActionModalCreature(creature);
  };

  const handleActionSubmit = async (creatureId: string, data: CreateActionRequestDto) => {
    await onAddAction(creatureId, data);
    // Actualizar criatura seleccionada si el modal de detalle está abierto
    const updated = creatures.find((c) => c.id === creatureId);
    if (updated) {
      setSelectedCreature(updated);
    }
  };

  const handleLearnMasteryAndSync = async (creatureId: string, masteryId: string) => {
    await onLearnMastery(creatureId, masteryId);
    const updated = creatures.find((c) => c.id === creatureId);
    if (updated) {
      setSelectedCreature(updated);
    }
  };

  return (
    <section className="bestiary-section">
      {/* Barra de Búsqueda y Filtros */}
      <div className="bestiary-controls">
        <div className="search-bar-wrapper">
          <span className="search-icon">🔍</span>
          <input
            id="creature-search-input"
            type="text"
            className="search-input"
            placeholder={t('bestiary.searchPlaceholder')}
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
          {searchTerm && (
            <button
              type="button"
              className="clear-search-btn"
              onClick={() => setSearchTerm('')}
            >
              ✕
            </button>
          )}
        </div>

        <div className="filter-dropdowns">
          <select
            id="filter-type-select"
            className="filter-select"
            value={selectedType}
            onChange={(e) => setSelectedType(e.target.value)}
          >
            <option value="">{t('bestiary.filterType')}</option>
            {CREATURE_TYPES.map((type) => (
              <option key={type} value={type}>{type}</option>
            ))}
          </select>

          <select
            id="filter-size-select"
            className="filter-select"
            value={selectedSize}
            onChange={(e) => setSelectedSize(e.target.value)}
          >
            <option value="">{t('bestiary.filterSize')}</option>
            {CREATURE_SIZES.map((size) => (
              <option key={size} value={size}>{size}</option>
            ))}
          </select>

          {(searchTerm || selectedSize || selectedType) && (
            <button
              type="button"
              id="clear-filters-btn"
              className="btn btn-outline btn-sm btn-clear-filters"
              onClick={handleClearFilters}
            >
              {t('bestiary.clearFilters')}
            </button>
          )}
        </div>
      </div>

      <div className="bestiary-count-bar">
        <span>{t('bestiary.totalCount', { count: filteredCreatures.length })}</span>
      </div>

      {/* Grid de Criaturas */}
      {filteredCreatures.length === 0 ? (
        <div className="empty-state-box">
          <div className="empty-icon">📜</div>
          <p className="empty-title">{t('bestiary.emptyState')}</p>
        </div>
      ) : (
        <div className="creatures-grid">
          {filteredCreatures.map((creature) => (
            <CreatureCard
              key={creature.id}
              creature={creature}
              onSelect={(c) => setSelectedCreature(c)}
              onDelete={handleDeleteWithConfirm}
            />
          ))}
        </div>
      )}

      {/* Modal Ficha Táctica Detallada */}
      <CreatureDetailModal
        creature={selectedCreature}
        allMasteries={allMasteries}
        onClose={() => setSelectedCreature(null)}
        onOpenAddAction={handleOpenAddActionFromDetail}
        onLearnMastery={handleLearnMasteryAndSync}
      />

      {/* Modal de Creación de Criatura */}
      <CreatureFormModal
        isOpen={isCreateModalOpen}
        onClose={onCloseCreateModal}
        onSubmit={onCreateCreature}
      />

      {/* Modal para Añadir Acción */}
      <ActionFormModal
        creature={actionModalCreature}
        allMasteries={allMasteries}
        isOpen={actionModalCreature !== null}
        onClose={() => setActionModalCreature(null)}
        onSubmit={handleActionSubmit}
      />
    </section>
  );
};
