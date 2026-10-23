import { HubConnection, HubConnectionBuilder, LogLevel, HubConnectionState } from '@microsoft/signalr';
import { apiClient } from './apiClient';
import type {
  StashItemDto,
  PartyFundDto,
  CraftingQueueItemDto,
  StashActivityLogDto,
  DowntimeAdvanceResultDto,
  PartyStashDto,
} from '../types/stash';

/**
 * Callbacks para los eventos emitidos en tiempo real por el StashHub.
 */
export interface StashHubCallbacks {
  onItemAdded?: (item: StashItemDto) => void;
  onItemDeleted?: (itemId: string) => void;
  onFundsUpdated?: (funds: PartyFundDto) => void;
  onCraftingQueueUpdated?: (queue: CraftingQueueItemDto[]) => void;
  onActivityLogged?: (log: StashActivityLogDto) => void;
  onDowntimeAdvanced?: (result: DowntimeAdvanceResultDto) => void;
  onStashUpdated?: (stash: PartyStashDto) => void;
}

/**
 * Servicio cliente de SignalR para la sincronización en vivo del alijo colaborativo,
 * Party Fund, cola de fabricación de Downtime y auditoría de transacciones.
 */
class StashHubService {
  private connection: HubConnection | null = null;
  private currentPartyId: string | null = null;
  private callbacks: StashHubCallbacks | null = null;

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('sparnarok_stash_external_event', (e: Event) => {
        const customEvent = e as CustomEvent;
        const { partyId, type, payload } = customEvent.detail || {};
        if (partyId && this.currentPartyId && partyId !== this.currentPartyId) return;
        if (type === 'ItemAdded' || type === 'ItemUpdated') {
          this.callbacks?.onItemAdded?.(payload);
        } else if (type === 'ItemDeleted') {
          this.callbacks?.onItemDeleted?.(payload);
        } else if (type === 'FundsUpdated' || type === 'FundChanged') {
          this.callbacks?.onFundsUpdated?.(payload);
        } else if (type === 'CraftingQueueUpdated') {
          this.callbacks?.onCraftingQueueUpdated?.(payload);
        } else if (type === 'ActivityLogged') {
          this.callbacks?.onActivityLogged?.(payload);
        } else if (type === 'DowntimeAdvanced') {
          this.callbacks?.onDowntimeAdvanced?.(payload);
        } else if (type === 'StashUpdated') {
          this.callbacks?.onStashUpdated?.(payload);
        }
      });
    }
  }

  /**
   * Inicia la conexión con el StashHub y se une al grupo de la Party seleccionada.
   */
  async startConnection(partyId: string, callbacks: StashHubCallbacks): Promise<void> {
    this.callbacks = callbacks;

    // Si ya existe una conexión activa, cambiar de grupo
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      if (this.currentPartyId && this.currentPartyId !== partyId) {
        try {
          await this.connection.invoke('LeavePartyGroup', this.currentPartyId);
        } catch (err) {
          console.warn('[StashHub] Error al abandonar grupo previo:', err);
        }
      }
      this.currentPartyId = partyId;
      try {
        await this.connection.invoke('JoinPartyGroup', partyId);
      } catch (err) {
        console.warn('[StashHub] Error al unirse al nuevo grupo de party:', err);
      }
      return;
    }

    if (this.connection) {
      try {
        await this.connection.stop();
      } catch {
        // no-op
      }
      this.connection = null;
    }

    this.currentPartyId = partyId;

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/stash', {
        accessTokenFactory: () => apiClient.getAuthToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('ItemAdded', (item: StashItemDto) => {
      this.callbacks?.onItemAdded?.(item);
    });

    connection.on('ItemDeleted', (itemId: string) => {
      this.callbacks?.onItemDeleted?.(itemId);
    });

    connection.on('FundsUpdated', (funds: PartyFundDto) => {
      this.callbacks?.onFundsUpdated?.(funds);
    });

    connection.on('FundChanged', (funds: PartyFundDto) => {
      this.callbacks?.onFundsUpdated?.(funds);
    });

    connection.on('ItemUpdated', (item: StashItemDto) => {
      this.callbacks?.onItemAdded?.(item);
    });

    connection.on('CraftingQueueUpdated', (queue: CraftingQueueItemDto[]) => {
      this.callbacks?.onCraftingQueueUpdated?.(queue);
    });

    connection.on('ActivityLogged', (log: StashActivityLogDto) => {
      this.callbacks?.onActivityLogged?.(log);
    });

    connection.on('DowntimeAdvanced', (result: DowntimeAdvanceResultDto) => {
      this.callbacks?.onDowntimeAdvanced?.(result);
    });

    connection.on('StashUpdated', (stash: PartyStashDto) => {
      this.callbacks?.onStashUpdated?.(stash);
    });

    connection.onreconnected(async () => {
      if (this.currentPartyId) {
        try {
          await connection.invoke('JoinPartyGroup', this.currentPartyId);
        } catch (err) {
          console.error('[StashHub] Error reconectando al grupo:', err);
        }
      }
    });

    try {
      await connection.start();
      await connection.invoke('JoinPartyGroup', partyId);
      this.connection = connection;
    } catch (err) {
      console.warn('[StashHub] Conexión SignalR no disponible (ejecución offline o fallback mock):', err);
      this.connection = null;
    }
  }

  /**
   * Desconecta del hub de SignalR y remueve los listeners.
   */
  async stopConnection(): Promise<void> {
    if (this.connection) {
      if (this.currentPartyId && this.connection.state === HubConnectionState.Connected) {
        try {
          await this.connection.invoke('LeavePartyGroup', this.currentPartyId);
        } catch {
          // no-op
        }
      }
      try {
        await this.connection.stop();
      } catch {
        // no-op
      }
      this.connection = null;
    }
    this.currentPartyId = null;
    this.callbacks = null;
  }
}

export const stashHubService = new StashHubService();
