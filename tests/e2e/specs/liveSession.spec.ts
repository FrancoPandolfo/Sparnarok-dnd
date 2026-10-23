import { test, expect } from '@playwright/test';

test.describe('Fase 3.6: Collaborative Live Play & Asymmetric Initiative Suite', () => {
  test('Two concurrent browser contexts: DM sees Goblin Boss and master controls, Player submits initiative and cannot see hidden Goblin Boss', async ({
    browser,
  }) => {
    // -------------------------------------------------------------
    // Context 1: Dungeon Master
    // -------------------------------------------------------------
    const dmContext = await browser.newContext();
    const dmPage = await dmContext.newPage();

    const initialCombatState = {
      partyId: 'party-alpha',
      partyName: 'La Compañía del Alba',
      status: 'WaitingForInitiative',
      currentRound: 1,
      currentTurnIndex: 0,
      combatants: [
        {
          id: 'hero-1',
          name: 'Kaelen Paladin',
          initiative: null,
          maxHp: 32,
          currentHp: 32,
          armorClass: 18,
          isPlayerCharacter: true,
          isVisibleToPlayers: true,
          assignedPlayerUserId: '00000000-0000-0000-0000-000000000002',
        },
        {
          id: 'monster-goblin-boss',
          name: 'Goblin Boss',
          initiative: 14,
          maxHp: 21,
          currentHp: 21,
          armorClass: 17,
          isPlayerCharacter: false,
          isVisibleToPlayers: false, // HIDDEN FROM PLAYERS (Anti-metagaming)
        },
      ],
    };

    await dmPage.addInitScript((state) => {
      window.localStorage.setItem(
        'sparnarok_auth',
        JSON.stringify({
          id: '00000000-0000-0000-0000-000000000001',
          email: 'dm@sparnarok.dnd',
          displayName: 'Dungeon Master',
          token: 'e2e-dm-token',
        })
      );
      window.localStorage.setItem('sparnarok_combat_party-alpha', JSON.stringify(state));
    }, initialCombatState);

    // -------------------------------------------------------------
    // Context 2: Player Hero
    // -------------------------------------------------------------
    const playerContext = await browser.newContext();
    const playerPage = await playerContext.newPage();

    await playerPage.addInitScript((state) => {
      window.localStorage.setItem(
        'sparnarok_auth',
        JSON.stringify({
          id: '00000000-0000-0000-0000-000000000002',
          email: 'player@sparnarok.dnd',
          displayName: 'Kaelen Player',
          token: 'e2e-player-token',
        })
      );
      window.localStorage.setItem('sparnarok_combat_party-alpha', JSON.stringify(state));
    }, initialCombatState);

    // Sincronización bidireccional entre contextos de navegador
    const syncState = async (partyId: string, state: any) => {
      await Promise.all([
        dmPage
          .evaluate(
            ({ partyId, state }) => {
              window.localStorage.setItem(`sparnarok_combat_${partyId}`, JSON.stringify(state));
              window.dispatchEvent(
                new CustomEvent('sparnarok_combat_external_update', { detail: { partyId } })
              );
            },
            { partyId, state }
          )
          .catch(() => {}),
        playerPage
          .evaluate(
            ({ partyId, state }) => {
              window.localStorage.setItem(`sparnarok_combat_${partyId}`, JSON.stringify(state));
              window.dispatchEvent(
                new CustomEvent('sparnarok_combat_external_update', { detail: { partyId } })
              );
            },
            { partyId, state }
          )
          .catch(() => {}),
      ]);
    };

    await dmPage.exposeFunction('__syncCombatState', syncState);
    await playerPage.exposeFunction('__syncCombatState', syncState);

    // 1. Navegar en la ventana del DM
    await dmPage.goto('/');
    await dmPage.locator('#tab-simulation-btn').click();

    // Abrir Modo Partida en la vista del DM
    const dmOpenLiveBtn = dmPage.locator('#open-live-session-btn');
    await expect(dmOpenLiveBtn).toBeVisible();
    await dmOpenLiveBtn.click();

    const dmModal = dmPage.locator('#live-session-modal-backdrop');
    await expect(dmModal).toBeVisible();

    // Asegurar que el DM tiene el rol de Dungeon Master
    const dmRoleBadge = dmPage.locator('#user-party-role-badge');
    await expect(dmRoleBadge).toContainText('Dungeon Master');

    // Verificar que el DM ve a Kaelen Paladin Y al Goblin Boss
    const dmTable = dmPage.locator('#initiative-order-table');
    await expect(dmTable).toContainText('Kaelen Paladin');
    await expect(dmTable).toContainText('Goblin Boss');
    await expect(dmTable).toContainText('🛡️ 17'); // CA del Goblin Boss visible para el DM

    // Verificar controles maestros del DM
    await expect(dmPage.locator('#auto-roll-monsters-btn')).toBeVisible();
    await expect(dmPage.locator('#start-combat-btn')).toBeVisible();

    // Pestaña de Notas de Sesión presente para el DM
    const dmNotesTab = dmPage.locator('#tab-session-notes');
    await expect(dmNotesTab).toBeVisible();

    // 2. Navegar en la ventana del Jugador
    await playerPage.goto('/');
    await playerPage.locator('#tab-simulation-btn').click();

    // Abrir Modo Partida en la vista del Jugador
    const playerOpenLiveBtn = playerPage.locator('#open-live-session-btn');
    await expect(playerOpenLiveBtn).toBeVisible();
    await playerOpenLiveBtn.click();

    // Conmutar a vista de Jugador si por defecto tomó DM
    const playerRoleBadge = playerPage.locator('#user-party-role-badge');
    const roleText = await playerRoleBadge.innerText();
    if (roleText.includes('Dungeon Master')) {
      await playerPage.locator('#toggle-role-test-btn').click();
    }
    await expect(playerPage.locator('#user-party-role-badge')).toContainText('Jugador');

    // Verificar que en la vista del Jugador NO aparece la pestaña de notas del DM
    await expect(playerPage.locator('#tab-session-notes')).not.toBeVisible();

    // Verificar que el Jugador ve a Kaelen Paladin pero NO VE al Goblin Boss (anti-metagaming estricto)
    const playerTable = playerPage.locator('#initiative-order-table');
    await expect(playerTable).toContainText('Kaelen Paladin');
    await expect(playerTable).not.toContainText('Goblin Boss');
    await expect(playerTable).not.toContainText('🛡️ 17'); // Sin fugas de CA de monstruos

    // 3. El Jugador ingresa su iniciativa
    const initiativeInput = playerPage.locator('#player-initiative-input');
    await expect(initiativeInput).toBeVisible();
    await initiativeInput.fill('19');

    const submitInitBtn = playerPage.locator('#submit-initiative-btn');
    await expect(submitInitBtn).toBeVisible();
    await submitInitBtn.click();

    // Verificar que la iniciativa 19 se refleja en la pantalla del Jugador
    await expect(playerTable).toContainText('19');

    // 4. Verificar que el DM en su pantalla también ve la iniciativa 19 reflejada
    await expect(dmTable).toContainText('19');
    await expect(dmTable).toContainText('Goblin Boss');

    // Cerrar contextos limpiamente
    await dmContext.close();
    await playerContext.close();
  });
});
