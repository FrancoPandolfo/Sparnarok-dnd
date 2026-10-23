import { test, expect } from '@playwright/test';

test.describe('Fase 3.9: Live Session Party Switching & Multi-User Onboarding Suite', () => {
  test('DM switches parties freely in dropdown with clean DOM resets and zero freeze, then second user joins via invite token', async ({
    browser,
  }) => {
    const partyAId = 'party-mithril-101';
    const partyBId = 'party-invierno-202';
    const inviteTokenA = 'token-mithril-invite-xyz';

    // -------------------------------------------------------------
    // Context 1: Dungeon Master (DM)
    // -------------------------------------------------------------
    const dmContext = await browser.newContext({
      permissions: ['clipboard-write', 'clipboard-read'],
    });
    const dmPage = await dmContext.newPage();

    // Capturar cualquier error no controlado o de red para asegurar cero freeze
    const consoleErrors: string[] = [];
    dmPage.on('pageerror', (err) => consoleErrors.push(err.message));

    const initialPartyA = {
      id: partyAId,
      name: 'La Vanguardia de Mithril',
      ownerUserId: '11111111-1111-1111-1111-111111111111',
      inviteToken: inviteTokenA,
      createdAt: new Date().toISOString(),
      members: [
        {
          id: 'c-hero-mithril',
          name: 'Sir Galahad',
          hitPoints: { max: 40, current: 40 },
          armorClass: { base: 18 },
          isPlayerCharacter: true,
        },
      ],
      partyMembers: [
        {
          id: 'pm-dm-1',
          partyId: partyAId,
          userId: '11111111-1111-1111-1111-111111111111',
          role: 'DungeonMaster',
          inGameName: 'El Gran DM',
          joinedAt: new Date().toISOString(),
        },
      ],
    };

    const initialPartyB = {
      id: partyBId,
      name: 'La Guardia de Invierno',
      ownerUserId: '11111111-1111-1111-1111-111111111111',
      inviteToken: 'token-invierno-invite-abc',
      createdAt: new Date().toISOString(),
      members: [
        {
          id: 'c-hero-winter',
          name: 'Frost Warden',
          hitPoints: { max: 35, current: 35 },
          armorClass: { base: 16 },
          isPlayerCharacter: true,
        },
      ],
      partyMembers: [
        {
          id: 'pm-dm-2',
          partyId: partyBId,
          userId: '11111111-1111-1111-1111-111111111111',
          role: 'DungeonMaster',
          inGameName: 'El Gran DM',
          joinedAt: new Date().toISOString(),
        },
      ],
    };

    const combatStateA = {
      partyId: partyAId,
      partyName: 'La Vanguardia de Mithril',
      status: 'InProgress',
      currentRound: 1,
      currentTurnIndex: 0,
      combatants: [
        {
          id: 'c-hero-mithril',
          name: 'Sir Galahad',
          initiative: 17,
          maxHp: 40,
          currentHp: 40,
          armorClass: 18,
          isPlayerCharacter: true,
          isVisibleToPlayers: true,
        },
      ],
    };

    const combatStateB = {
      partyId: partyBId,
      partyName: 'La Guardia de Invierno',
      status: 'InProgress',
      currentRound: 2,
      currentTurnIndex: 0,
      combatants: [
        {
          id: 'c-hero-winter',
          name: 'Frost Warden',
          initiative: 14,
          maxHp: 35,
          currentHp: 28,
          armorClass: 16,
          isPlayerCharacter: true,
          isVisibleToPlayers: true,
        },
      ],
    };

    await dmPage.addInitScript(
      ({ pA, pB, cA, cB }) => {
        window.localStorage.setItem(
          'sparnarok_auth',
          JSON.stringify({
            id: '11111111-1111-1111-1111-111111111111',
            email: 'dm@sparnarok.dnd',
            displayName: 'Dungeon Master',
            globalNickname: 'El Gran DM',
            token: 'jwt-dm-token',
          })
        );
        window.localStorage.setItem('sparnarok_parties', JSON.stringify([pA, pB]));
        window.localStorage.setItem(`sparnarok_combat_${pA.id}`, JSON.stringify(cA));
        window.localStorage.setItem(`sparnarok_combat_${pB.id}`, JSON.stringify(cB));
      },
      { pA: initialPartyA, pB: initialPartyB, cA: combatStateA, cB: combatStateB }
    );

    // 1. Navegar Usuario A a la aplicación y abrir Live Session Modal
    await dmPage.goto('/');
    await dmPage.locator('#tab-simulation-btn').click();

    const openLiveBtn = dmPage.locator('#open-live-session-btn');
    await expect(openLiveBtn).toBeVisible();
    await openLiveBtn.click();

    const modalBackdrop = dmPage.locator('#live-session-modal-backdrop');
    await expect(modalBackdrop).toBeVisible();

    // 2. Comprobar que Party A carga o se inicializa correctamente
    const partySelect = dmPage.locator('#live-session-party-select');
    await partySelect.selectOption(partyAId);

    // Si el combate no está iniciado en el Hub, inicializarlo con los miembros de Party A
    const initCombatBtnA = dmPage.locator('#init-live-combat-btn');
    if (await initCombatBtnA.isVisible()) {
      await initCombatBtnA.click();
    }

    const trackerTable = dmPage.locator('#initiative-order-table');
    await expect(trackerTable).toBeVisible();
    await expect(trackerTable).toContainText('Sir Galahad');

    // 3. CAMBIO DE PARTY: Cambiar a Party B en el selector
    await partySelect.selectOption(partyBId);

    // Inicializar Party B si está pendiente
    const initCombatBtnB = dmPage.locator('#init-live-combat-btn');
    if (await initCombatBtnB.isVisible()) {
      await initCombatBtnB.click();
    }

    // Verificar que el DOM se reinicia y muestra los combatientes de Party B sin residuos de Party A
    await expect(trackerTable).toBeVisible();
    await expect(trackerTable).toContainText('Frost Warden');
    await expect(trackerTable).not.toContainText('Sir Galahad');

    // 4. CAMBIO A MODO RÁPIDO (Sin Party)
    await partySelect.selectOption('quick-session');

    // En Modo Rápido, el tracker debe mostrarse o permitir añadir combatiente rápido
    const quickCombatTracker = dmPage.locator('#live-combat-tracker');
    await expect(quickCombatTracker).toBeVisible();

    // Añadir combatiente rápido en Modo Rápido
    const addQuickBtn = dmPage.locator('#add-quick-combatant-btn');
    await expect(addQuickBtn).toBeVisible();
    await addQuickBtn.click();

    const quickModal = dmPage.locator('#quick-combatant-modal');
    await expect(quickModal).toBeVisible();

    await dmPage.locator('#quick-combatant-name').fill('Bandido Nocturno');
    await dmPage.locator('#quick-combatant-hp').fill('22');
    await dmPage.locator('#quick-combatant-ac').fill('13');
    await dmPage.locator('#quick-combatant-submit-btn').click();

    await expect(quickModal).not.toBeVisible();
    await expect(dmPage.locator('#initiative-order-table')).toContainText('Bandido Nocturno');

    // Verificar pestañas de notas en modo local sin fallos
    const notesTab = dmPage.locator('#tab-session-notes');
    await notesTab.click();
    await expect(dmPage.locator('#session-notes-editor-panel')).toBeVisible();

    // Volver a la pestaña de Tracker
    await dmPage.locator('#tab-live-combat').click();
    await expect(dmPage.locator('#initiative-order-table')).toBeVisible();

    // 5. REGRESAR A PARTY A Y COPIAR ENLACE DE INVITACIÓN
    await partySelect.selectOption(partyAId);
    await expect(trackerTable).toContainText('Sir Galahad');

    const copyInviteBtn = dmPage.locator('#copy-invite-link-btn');
    await expect(copyInviteBtn).toBeVisible();
    await copyInviteBtn.click();
    await expect(copyInviteBtn).toContainText('✓ Enlace Copiado');

    // Cero errores de ejecución o red en DM
    expect(consoleErrors).toHaveLength(0);

    // -------------------------------------------------------------
    // Context 2: Usuario B (Player - Onboarding e Integración)
    // -------------------------------------------------------------
    const playerContext = await browser.newContext();
    const playerPage = await playerContext.newPage();

    await playerPage.addInitScript(
      ({ pA }) => {
        window.localStorage.setItem(
          'sparnarok_auth',
          JSON.stringify({
            id: '22222222-2222-2222-2222-222222222222',
            email: 'player2@sparnarok.dnd',
            displayName: 'Jugador Aventurero',
            globalNickname: 'Gimli',
            token: 'jwt-player-token',
          })
        );
        window.localStorage.setItem('sparnarok_parties', JSON.stringify([pA]));
      },
      { pA: initialPartyA }
    );

    // Usuario B abre la invitación directamente por URL
    await playerPage.goto(`/?inviteToken=${inviteTokenA}`);

    // Modal de Onboarding se abre automáticamente
    const onboardingModal = playerPage.locator('#party-onboarding-modal-backdrop');
    await expect(onboardingModal).toBeVisible();

    await expect(playerPage.locator('#invited-party-name')).toContainText('La Vanguardia de Mithril');

    // Ingresar nombre de personaje y confirmar
    await playerPage.locator('#onboarding-ingame-name').fill('Gimli');
    await playerPage.locator('#confirm-join-party-btn').click();

    // El modal de onboarding se cierra y el Live Session se abre para el Jugador
    await expect(onboardingModal).not.toBeVisible();
    const playerLiveModal = playerPage.locator('#live-session-modal-backdrop');
    await expect(playerLiveModal).toBeVisible();
    await expect(playerLiveModal).toContainText('La Vanguardia de Mithril');

    // Cerrar contextos limpiamente
    await dmContext.close();
    await playerContext.close();
  });
});
