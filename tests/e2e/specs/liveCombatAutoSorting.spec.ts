import { test, expect } from '@playwright/test';

test.describe('Live Combat Auto-Sorting Suite (PushNullsToBottom & KeepNullsInPlace)', () => {
  test('DM uses explicit auto-sort buttons to sort mixed initiatives with nulls, updating both DM and Player views in real time', async ({
    browser,
  }) => {
    const partyId = 'party-autosort-e2e';
    const dmUserId = '11111111-1111-1111-1111-111111111111';
    const playerUserId = '22222222-2222-2222-2222-222222222222';

    const partyData = {
      id: partyId,
      name: 'La Compañía del Orden',
      ownerUserId: dmUserId,
      createdAt: new Date().toISOString(),
      members: [],
      partyMembers: [
        {
          id: 'pm-dm',
          partyId,
          userId: dmUserId,
          role: 'DungeonMaster',
          inGameName: 'El Dungeon Master',
          joinedAt: new Date().toISOString(),
        },
        {
          id: 'pm-player',
          partyId,
          userId: playerUserId,
          role: 'Player',
          inGameName: 'Kaelen Paladin',
          joinedAt: new Date().toISOString(),
        },
      ],
    };

    // Estado inicial con iniciativas mixtas: [Null, 10, Null, 20]
    const initialCombatState = {
      partyId,
      partyName: 'La Compañía del Orden',
      status: 'WaitingForInitiative',
      currentRound: 1,
      currentTurnIndex: 0,
      combatants: [
        {
          id: 'combatant-0-null',
          name: 'Hero Pending',
          initiative: null,
          maxHp: 25,
          currentHp: 25,
          armorClass: 15,
          isPlayerCharacter: true,
          isVisibleToPlayers: true,
          sortOrder: 0,
        },
        {
          id: 'combatant-1-num10',
          name: 'Goblin Archer',
          initiative: 10,
          maxHp: 12,
          currentHp: 12,
          armorClass: 13,
          isPlayerCharacter: false,
          isVisibleToPlayers: true,
          sortOrder: 1,
        },
        {
          id: 'combatant-2-null',
          name: 'Monster Lurker',
          initiative: null,
          maxHp: 18,
          currentHp: 18,
          armorClass: 14,
          isPlayerCharacter: false,
          isVisibleToPlayers: true,
          sortOrder: 2,
        },
        {
          id: 'combatant-3-num20',
          name: 'Kaelen Paladin',
          initiative: 20,
          maxHp: 30,
          currentHp: 30,
          armorClass: 18,
          isPlayerCharacter: true,
          isVisibleToPlayers: true,
          assignedPlayerUserId: playerUserId,
          sortOrder: 3,
        },
      ],
    };

    // -------------------------------------------------------------
    // Context 1: Dungeon Master
    // -------------------------------------------------------------
    const dmContext = await browser.newContext();
    const dmPage = await dmContext.newPage();

    await dmPage.addInitScript(
      ({ partyData, combatState, dmUserId }) => {
        window.localStorage.setItem(
          'sparnarok_auth',
          JSON.stringify({
            id: dmUserId,
            email: 'dm@autosort.dnd',
            displayName: 'El Dungeon Master',
            token: 'jwt-dm-token',
          })
        );
        window.localStorage.setItem('sparnarok_parties', JSON.stringify([partyData]));
        window.localStorage.setItem(`sparnarok_combat_${partyData.id}`, JSON.stringify(combatState));
      },
      { partyData, combatState: initialCombatState, dmUserId }
    );

    // -------------------------------------------------------------
    // Context 2: Jugador (Kaelen Paladin)
    // -------------------------------------------------------------
    const playerContext = await browser.newContext();
    const playerPage = await playerContext.newPage();

    await playerPage.addInitScript(
      ({ partyData, combatState, playerUserId }) => {
        window.localStorage.setItem(
          'sparnarok_auth',
          JSON.stringify({
            id: playerUserId,
            email: 'kaelen@autosort.dnd',
            displayName: 'Kaelen Paladin',
            token: 'jwt-player-token',
          })
        );
        window.localStorage.setItem('sparnarok_parties', JSON.stringify([partyData]));
        window.localStorage.setItem(`sparnarok_combat_${partyData.id}`, JSON.stringify(combatState));
      },
      { partyData, combatState: initialCombatState, playerUserId }
    );

    // Bridge de sincronización bidireccional entre contextos
    await dmPage.exposeFunction('__forwardToPlayer', async (type: string, payload: any) => {
      await playerPage.evaluate(
        ({ type, payload }) => {
          (window as any).__isSyncing = true;
          try {
            if (type === 'combat') {
              window.localStorage.setItem(`sparnarok_combat_${payload.partyId}`, JSON.stringify(payload.state));
              window.dispatchEvent(
                new CustomEvent('sparnarok_combat_external_update', { detail: { partyId: payload.partyId } })
              );
            }
          } finally {
            (window as any).__isSyncing = false;
          }
        },
        { type, payload }
      ).catch(() => {});
    });

    await playerPage.exposeFunction('__forwardToDm', async (type: string, payload: any) => {
      await dmPage.evaluate(
        ({ type, payload }) => {
          (window as any).__isSyncing = true;
          try {
            if (type === 'combat') {
              window.localStorage.setItem(`sparnarok_combat_${payload.partyId}`, JSON.stringify(payload.state));
              window.dispatchEvent(
                new CustomEvent('sparnarok_combat_external_update', { detail: { partyId: payload.partyId } })
              );
            }
          } finally {
            (window as any).__isSyncing = false;
          }
        },
        { type, payload }
      ).catch(() => {});
    });

    await dmPage.addInitScript(() => {
      const orig = window.localStorage.setItem.bind(window.localStorage);
      window.localStorage.setItem = function (key, val) {
        orig(key, val);
        if ((window as any).__isSyncing) return;
        try {
          if (key.startsWith('sparnarok_combat_')) {
            const pId = key.replace('sparnarok_combat_', '');
            (window as any).__forwardToPlayer?.('combat', { partyId: pId, state: JSON.parse(val) });
          }
        } catch {}
      };
    });

    await playerPage.addInitScript(() => {
      const orig = window.localStorage.setItem.bind(window.localStorage);
      window.localStorage.setItem = function (key, val) {
        orig(key, val);
        if ((window as any).__isSyncing) return;
        try {
          if (key.startsWith('sparnarok_combat_')) {
            const pId = key.replace('sparnarok_combat_', '');
            (window as any).__forwardToDm?.('combat', { partyId: pId, state: JSON.parse(val) });
          }
        } catch {}
      };
    });

    // 1. DM abre la sesión en vivo y selecciona la Party
    await dmPage.goto('/');
    await dmPage.locator('#tab-simulation-btn').click();
    await dmPage.locator('#open-live-session-btn').click();
    await expect(dmPage.locator('#live-session-modal-backdrop')).toBeVisible();

    const dmPartySelect = dmPage.locator('#live-session-party-select');
    await dmPartySelect.selectOption({ label: 'La Compañía del Orden' });
    await expect(dmPage.locator('#user-party-role-badge')).toContainText('Dungeon Master');

    // 2. Jugador abre la sesión en vivo y selecciona la Party
    await playerPage.goto('/');
    await playerPage.locator('#tab-simulation-btn').click();
    await playerPage.locator('#open-live-session-btn').click();
    await expect(playerPage.locator('#live-session-modal-backdrop')).toBeVisible();

    const playerPartySelect = playerPage.locator('#live-session-party-select');
    await playerPartySelect.selectOption({ label: 'La Compañía del Orden' });
    await expect(playerPage.locator('#user-party-role-badge')).toContainText('Jugador');

    // Verificar que en la vista del Jugador NO existe el botón de auto-ordenar
    await expect(playerPage.locator('#auto-sort-dropdown-btn')).toHaveCount(0);

    // 3. Validar orden inicial en ambas tablas [Null, 10, Null, 20]
    const dmRows = dmPage.locator('#initiative-order-table tbody tr');
    const playerRows = playerPage.locator('#initiative-order-table tbody tr');

    await expect(dmRows).toHaveCount(4);
    await expect(playerRows).toHaveCount(4);

    await expect(dmRows.nth(0)).toContainText('Hero Pending');
    await expect(dmRows.nth(1)).toContainText('Goblin Archer');
    await expect(dmRows.nth(2)).toContainText('Monster Lurker');
    await expect(dmRows.nth(3)).toContainText('Kaelen Paladin');

    // -------------------------------------------------------------
    // 4. Probar Algoritmo: KeepNullsInPlace [Null, 10, Null, 20] -> [Null, 20, Null, 10]
    // -------------------------------------------------------------
    const sortDropdownBtn = dmPage.locator('#auto-sort-dropdown-btn');
    await expect(sortDropdownBtn).toBeVisible();
    await sortDropdownBtn.click();

    const sortMenu = dmPage.locator('#auto-sort-dropdown-menu');
    await expect(sortMenu).toBeVisible();

    const keepNullsBtn = dmPage.locator('#sort-keep-nulls-place-btn');
    await expect(keepNullsBtn).toBeVisible();
    await expect(keepNullsBtn).toContainText('Ordenar (Mantener vacíos en posición)');
    await keepNullsBtn.click();

    // El menú debe cerrarse tras seleccionar
    await expect(sortMenu).not.toBeVisible();

    // Validar orden resultante en DM: [Hero Pending (Null), Kaelen Paladin (20), Monster Lurker (Null), Goblin Archer (10)]
    await expect(dmRows.nth(0)).toContainText('Hero Pending');
    await expect(dmRows.nth(1)).toContainText('Kaelen Paladin');
    await expect(dmRows.nth(2)).toContainText('Monster Lurker');
    await expect(dmRows.nth(3)).toContainText('Goblin Archer');

    // Validar que la UI del Jugador se sincronizó al mismo orden exacto
    await expect(playerRows.nth(0)).toContainText('Hero Pending');
    await expect(playerRows.nth(1)).toContainText('Kaelen Paladin');
    await expect(playerRows.nth(2)).toContainText('Monster Lurker');
    await expect(playerRows.nth(3)).toContainText('Goblin Archer');

    // -------------------------------------------------------------
    // 5. Probar Algoritmo: PushNullsToBottom -> [20, 10, Null, Null]
    // -------------------------------------------------------------
    await sortDropdownBtn.click();
    await expect(sortMenu).toBeVisible();

    const pushNullsBtn = dmPage.locator('#sort-push-nulls-bottom-btn');
    await expect(pushNullsBtn).toBeVisible();
    await expect(pushNullsBtn).toContainText('Ordenar (Mover vacíos al final)');
    await pushNullsBtn.click();

    await expect(sortMenu).not.toBeVisible();

    // Validar orden resultante en DM: [Kaelen Paladin (20), Goblin Archer (10), Hero Pending (Null), Monster Lurker (Null)]
    await expect(dmRows.nth(0)).toContainText('Kaelen Paladin');
    await expect(dmRows.nth(1)).toContainText('Goblin Archer');
    await expect(dmRows.nth(2)).toContainText('Hero Pending');
    await expect(dmRows.nth(3)).toContainText('Monster Lurker');

    // Validar que la UI del Jugador refleja el mismo orden
    await expect(playerRows.nth(0)).toContainText('Kaelen Paladin');
    await expect(playerRows.nth(1)).toContainText('Goblin Archer');
    await expect(playerRows.nth(2)).toContainText('Hero Pending');
    await expect(playerRows.nth(3)).toContainText('Monster Lurker');
  });
});
