import { test, expect } from '@playwright/test';

test.describe('Fase 3.5 & 3.7: Party Stash Multiplayer Real-Time Synchronization Suite', () => {
  test('DM adds "Espada Larga" and modifies gold in Party Fund; Player view in Live Combat stash tab updates in real-time without page reload', async ({
    browser,
  }) => {
    const partyId = 'party-stash-mp-e2e';
    const dmUserId = '11111111-1111-1111-1111-111111111111';
    const playerUserId = '22222222-2222-2222-2222-222222222222';

    const partyData = {
      id: partyId,
      name: 'La Fraternidad del Fénix',
      ownerUserId: dmUserId,
      createdAt: new Date().toISOString(),
      members: [
        {
          id: 'c-hero-paladin',
          name: 'Sir Arthur',
          hitPoints: { max: 35, current: 35 },
          armorClass: { base: 18 },
          isPlayerCharacter: true,
        },
      ],
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
          inGameName: 'Valeros',
          joinedAt: new Date().toISOString(),
        },
      ],
    };

    const initialCombatState = {
      partyId,
      partyName: 'La Fraternidad del Fénix',
      status: 'InProgress',
      currentRound: 1,
      currentTurnIndex: 0,
      combatants: [
        {
          id: 'c-hero-paladin',
          name: 'Sir Arthur',
          initiative: 15,
          maxHp: 35,
          currentHp: 35,
          armorClass: 18,
          isPlayerCharacter: true,
          isVisibleToPlayers: true,
        },
      ],
    };

    const initialFunds = {
      partyId,
      copperPieces: 0,
      silverPieces: 0,
      electrumPieces: 0,
      goldPieces: 100,
      platinumPieces: 0,
      totalGoldEquivalent: 100,
    };

    // -------------------------------------------------------------
    // Context 1: Dungeon Master
    // -------------------------------------------------------------
    const dmContext = await browser.newContext();
    const dmPage = await dmContext.newPage();

    await dmPage.addInitScript(
      ({ partyData, combatState, funds, dmUserId }) => {
        window.localStorage.setItem(
          'sparnarok_auth',
          JSON.stringify({
            id: dmUserId,
            email: 'dm@fenix.dnd',
            displayName: 'El Dungeon Master',
            token: 'jwt-dm-token',
          })
        );
        window.localStorage.setItem('sparnarok_parties', JSON.stringify([partyData]));
        window.localStorage.setItem(`sparnarok_combat_${partyData.id}`, JSON.stringify(combatState));
        window.localStorage.setItem(`sparnarok_funds_${partyData.id}`, JSON.stringify(funds));
        window.localStorage.setItem(`sparnarok_stash_${partyData.id}`, JSON.stringify([]));
      },
      { partyData, combatState: initialCombatState, funds: initialFunds, dmUserId }
    );

    // -------------------------------------------------------------
    // Context 2: Player (Valeros)
    // -------------------------------------------------------------
    const playerContext = await browser.newContext();
    const playerPage = await playerContext.newPage();

    await playerPage.addInitScript(
      ({ partyData, combatState, funds, playerUserId }) => {
        window.localStorage.setItem(
          'sparnarok_auth',
          JSON.stringify({
            id: playerUserId,
            email: 'valeros@fenix.dnd',
            displayName: 'Valeros',
            token: 'jwt-player-token',
          })
        );
        window.localStorage.setItem('sparnarok_parties', JSON.stringify([partyData]));
        window.localStorage.setItem(`sparnarok_combat_${partyData.id}`, JSON.stringify(combatState));
        window.localStorage.setItem(`sparnarok_funds_${partyData.id}`, JSON.stringify(funds));
        window.localStorage.setItem(`sparnarok_stash_${partyData.id}`, JSON.stringify([]));
      },
      { partyData, combatState: initialCombatState, funds: initialFunds, playerUserId }
    );

    // Inter-context bridge for Stash and Funds synchronization
    await dmPage.exposeFunction('__forwardStashToPlayer', async (detail: any) => {
      await playerPage.evaluate((evtDetail) => {
        (window as any).__isSyncing = true;
        try {
          if (evtDetail.type === 'ItemAdded') {
            const current = JSON.parse(window.localStorage.getItem(`sparnarok_stash_${evtDetail.partyId}`) || '[]');
            const updated = [evtDetail.payload, ...current.filter((i: any) => i.id !== evtDetail.payload.id)];
            window.localStorage.setItem(`sparnarok_stash_${evtDetail.partyId}`, JSON.stringify(updated));
          } else if (evtDetail.type === 'FundsUpdated') {
            window.localStorage.setItem(`sparnarok_funds_${evtDetail.partyId}`, JSON.stringify(evtDetail.payload));
          }
          window.dispatchEvent(new CustomEvent('sparnarok_stash_external_event', { detail: evtDetail }));
        } finally {
          (window as any).__isSyncing = false;
        }
      }, detail).catch(() => {});
    });

    await playerPage.exposeFunction('__forwardStashToDm', async (detail: any) => {
      await dmPage.evaluate((evtDetail) => {
        (window as any).__isSyncing = true;
        try {
          if (evtDetail.type === 'ItemAdded') {
            const current = JSON.parse(window.localStorage.getItem(`sparnarok_stash_${evtDetail.partyId}`) || '[]');
            const updated = [evtDetail.payload, ...current.filter((i: any) => i.id !== evtDetail.payload.id)];
            window.localStorage.setItem(`sparnarok_stash_${evtDetail.partyId}`, JSON.stringify(updated));
          } else if (evtDetail.type === 'FundsUpdated') {
            window.localStorage.setItem(`sparnarok_funds_${evtDetail.partyId}`, JSON.stringify(evtDetail.payload));
          }
          window.dispatchEvent(new CustomEvent('sparnarok_stash_external_event', { detail: evtDetail }));
        } finally {
          (window as any).__isSyncing = false;
        }
      }, detail).catch(() => {});
    });

    await dmPage.addInitScript(() => {
      window.addEventListener('sparnarok_stash_external_event', (e: Event) => {
        if ((window as any).__isSyncing) return;
        const detail = (e as CustomEvent).detail;
        if (detail) {
          (window as any).__forwardStashToPlayer?.(detail);
        }
      });
    });

    await playerPage.addInitScript(() => {
      window.addEventListener('sparnarok_stash_external_event', (e: Event) => {
        if ((window as any).__isSyncing) return;
        const detail = (e as CustomEvent).detail;
        if (detail) {
          (window as any).__forwardStashToDm?.(detail);
        }
      });
    });

    // 1. DM abre la aplicación y abre el Stash desde el botón de la Navbar (#open-stash-btn)
    await dmPage.goto('/');
    const dmNavStashBtn = dmPage.locator('#open-stash-btn');
    await expect(dmNavStashBtn).toBeVisible();
    await dmNavStashBtn.click();

    const dmStashModal = dmPage.locator('#party-stash-modal');
    await expect(dmStashModal).toBeVisible();
    await expect(dmPage.locator('#fund-gp-amount')).toHaveText('100');

    // 2. Jugador abre la aplicación y abre la sesión en vivo (Modo Partida)
    await playerPage.goto('/');
    await playerPage.locator('#tab-simulation-btn').click();
    await playerPage.locator('#open-live-session-btn').click();
    await expect(playerPage.locator('#live-session-modal-backdrop')).toBeVisible();

    // 3. Jugador hace clic en la 3ra pestaña: "#tab-session-stash" ("Botín de Grupo")
    const playerStashTab = playerPage.locator('#tab-session-stash');
    await expect(playerStashTab).toBeVisible();
    await playerStashTab.click();

    // 4. Verificar que el Stash se incrusta en Modo Partida y muestra 100 PO y 0.0 lbs iniciales
    await expect(playerPage.locator('#pane-session-stash')).toBeVisible();
    await expect(playerPage.locator('#pane-session-stash #party-stash-modal')).toBeVisible();
    await expect(playerPage.locator('#pane-session-stash #fund-gp-amount')).toHaveText('100');
    await expect(playerPage.locator('#pane-session-stash #stash-total-weight')).toContainText('0.0 lbs');

    // 5. DM añade el objeto "Espada Larga" (Cantidad: 2, Peso: 3 lbs)
    await dmPage.locator('#open-add-stash-item-btn').click();
    await expect(dmPage.locator('#add-stash-item-modal')).toBeVisible();

    await dmPage.locator('#stash-item-name-input').fill('Espada Larga');
    await dmPage.locator('#stash-item-quantity-input').fill('2');
    await dmPage.locator('#stash-item-weight-input').fill('3');
    await dmPage.locator('#stash-item-type-input').fill('Weapons');
    await dmPage.locator('#stash-item-description-input').fill('Espada forjada con acero de las Tierras Altas.');
    await dmPage.locator('#save-stash-item-btn').click();

    // 6. Verificar que en la vista del DM aparece "Espada Larga" y el peso total acumulado es 6.0 lbs
    await expect(dmPage.locator('.item-title', { hasText: 'Espada Larga' })).toBeVisible();
    await expect(dmPage.locator('#stash-total-weight')).toContainText('6.0 lbs');

    // 7. CRÍTICO: En la vista del Jugador (en el otro contexto), "Espada Larga" debe aparecer en tiempo real
    // SIN recargar la página (F5)
    const playerItemTitle = playerPage.locator('#pane-session-stash .item-title', { hasText: 'Espada Larga' });
    await expect(playerItemTitle).toBeVisible();
    await expect(playerPage.locator('#pane-session-stash #stash-total-weight')).toContainText('6.0 lbs');

    // 8. DM modifica las monedas en el Party Fund: cambia Oro a 350 GP
    await dmPage.locator('#toggle-edit-funds-btn').click();
    await expect(dmPage.locator('#edit-party-funds-form')).toBeVisible();

    const goldInput = dmPage.locator('#funds-gp');
    await goldInput.fill('350');
    await dmPage.locator('#save-funds-btn').click();

    // 9. Verificar que el DM ve 350 PO
    await expect(dmPage.locator('#fund-gp-amount')).toHaveText('350');

    // 10. CRÍTICO: En la vista del Jugador, el Party Fund debe actualizarse inmediatamente a 350 PO
    // SIN recargar la página
    await expect(playerPage.locator('#pane-session-stash #fund-gp-amount')).toHaveText('350');
  });
});
