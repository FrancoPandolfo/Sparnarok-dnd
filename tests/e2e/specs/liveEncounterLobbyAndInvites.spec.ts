import { test, expect } from '@playwright/test';

test.describe('Live Encounter Lobby, Combat Invites, and Session Notes Persistence Suite', () => {
  test('DM starts encounter without player; Player enters lobby; DM invites Player; Player accepts and joins initiative in real time; Notes persist across reload (F5)', async ({
    browser,
  }) => {
    const partyId = 'party-lobby-e2e';
    const dmUserId = '11111111-1111-1111-1111-111111111111';
    const playerUserId = '22222222-2222-2222-2222-222222222222';

    const partyData = {
      id: partyId,
      name: 'La Compañía del Bosque',
      ownerUserId: dmUserId,
      createdAt: new Date().toISOString(),
      members: [],
      partyMembers: [
        {
          id: 'pm-dm',
          partyId,
          userId: dmUserId,
          role: 'DungeonMaster',
          inGameName: 'El Gran DM',
          joinedAt: new Date().toISOString(),
        },
        {
          id: 'pm-player',
          partyId,
          userId: playerUserId,
          role: 'Player',
          inGameName: 'Legolas',
          joinedAt: new Date().toISOString(),
        },
      ],
    };

    const initialCombatState = {
      partyId,
      partyName: 'La Compañía del Bosque',
      status: 'WaitingForInitiative',
      currentRound: 1,
      currentTurnIndex: 0,
      combatants: [
        {
          id: 'monster-orc-1',
          name: 'Orco Berserker',
          initiative: 12,
          maxHp: 30,
          currentHp: 30,
          armorClass: 13,
          isPlayerCharacter: false,
          isVisibleToPlayers: true,
          sortOrder: 0,
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
            email: 'dm@bosque.dnd',
            displayName: 'El Gran DM',
            token: 'jwt-dm-token',
          })
        );
        window.localStorage.setItem('sparnarok_parties', JSON.stringify([partyData]));
        window.localStorage.setItem(`sparnarok_combat_${partyData.id}`, JSON.stringify(combatState));
      },
      { partyData, combatState: initialCombatState, dmUserId }
    );

    // -------------------------------------------------------------
    // Context 2: Jugador (Legolas)
    // -------------------------------------------------------------
    const playerContext = await browser.newContext();
    const playerPage = await playerContext.newPage();

    await playerPage.addInitScript(
      ({ partyData, combatState, playerUserId }) => {
        window.localStorage.setItem(
          'sparnarok_auth',
          JSON.stringify({
            id: playerUserId,
            email: 'legolas@bosque.dnd',
            displayName: 'Legolas Hojaverde',
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
            } else if (type === 'invite') {
              window.localStorage.setItem('sparnarok_combat_invite', JSON.stringify(payload));
              window.dispatchEvent(new CustomEvent('sparnarok_combat_invite_local', { detail: payload }));
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
          if (key === 'sparnarok_combat_invite') {
            (window as any).__forwardToPlayer?.('invite', JSON.parse(val));
          } else if (key.startsWith('sparnarok_combat_')) {
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

    // 1. DM abre la sesión y selecciona la party
    await dmPage.goto('/');
    await dmPage.locator('#tab-simulation-btn').click();
    await dmPage.locator('#open-live-session-btn').click();
    await expect(dmPage.locator('#live-session-modal-backdrop')).toBeVisible();

    const dmPartySelect = dmPage.locator('#live-session-party-select');
    await dmPartySelect.selectOption({ label: 'La Compañía del Bosque' });
    await expect(dmPage.locator('#user-party-role-badge')).toContainText('Dungeon Master');

    // 2. Jugador abre la sesión y selecciona la party
    await playerPage.goto('/');
    await playerPage.locator('#tab-simulation-btn').click();
    await playerPage.locator('#open-live-session-btn').click();
    await expect(playerPage.locator('#live-session-modal-backdrop')).toBeVisible();

    const playerPartySelect = playerPage.locator('#live-session-party-select');
    await playerPartySelect.selectOption({ label: 'La Compañía del Bosque' });
    await expect(playerPage.locator('#user-party-role-badge')).toContainText('Jugador');

    // 3. Verificación de Lobby del Jugador:
    // El combate está activo con el Orco, pero Legolas no está en la lista de iniciativa
    const playerLobby = playerPage.locator('#player-combat-lobby');
    await expect(playerLobby).toBeVisible();
    await expect(playerLobby).toContainText('Un encuentro está en curso');
    await expect(playerPage.locator('#join-encounter-lobby-btn')).toBeVisible();

    // 4. En la vista del DM:
    // Abre el panel "Miembros de la Party"
    const toggleMembersBtn = dmPage.locator('#toggle-party-members-btn');
    await expect(toggleMembersBtn).toBeVisible();
    await toggleMembersBtn.click();

    const membersPanel = dmPage.locator('#party-members-invite-panel');
    await expect(membersPanel).toBeVisible();
    await expect(membersPanel).toContainText('Legolas');

    const inviteLegolasBtn = dmPage.locator(`#invite-member-btn-${playerUserId}`);
    await expect(inviteLegolasBtn).toBeVisible();
    await inviteLegolasBtn.click();

    // Feedback en la UI del DM
    await expect(inviteLegolasBtn).toContainText('Invitado');

    // 5. En la vista del Jugador:
    // Aparece el modal de invitación a combate
    const inviteModal = playerPage.locator('#combat-invite-modal');
    await expect(inviteModal).toBeVisible();
    await expect(inviteModal).toContainText('El Dungeon Master te invita a unirte al combate actual');

    const acceptInviteBtn = playerPage.locator('#accept-combat-invite-btn');
    await expect(acceptInviteBtn).toBeVisible();
    await acceptInviteBtn.click();

    // El modal de invitación se cierra
    await expect(inviteModal).not.toBeVisible();

    // 6. El Jugador aparece instantáneamente en la lista de iniciativa de AMBOS navegadores
    const dmInitiativeTable = dmPage.locator('#initiative-order-table');
    const playerInitiativeTable = playerPage.locator('#initiative-order-table');

    await expect(playerInitiativeTable).toBeVisible();
    await expect(playerInitiativeTable).toContainText('Legolas');

    await expect(dmInitiativeTable).toBeVisible();
    await expect(dmInitiativeTable).toContainText('Legolas');

    // -------------------------------------------------------------
    // 7. Auditoría y Persistencia de Notas de Sesión (F5 Reload)
    // -------------------------------------------------------------
    // Navegar a la pestaña "📜 Mis Notas Privadas" en el DM
    const dmNotesTab = dmPage.locator('#tab-session-notes');
    await expect(dmNotesTab).toBeVisible();
    await dmNotesTab.click();

    // Redactar y guardar una nota
    const newNoteBtn = dmPage.locator('#new-session-note-btn');
    await expect(newNoteBtn).toBeVisible();
    await newNoteBtn.click();

    const noteTitleInput = dmPage.locator('#session-note-title-input');
    await noteTitleInput.fill('Estrategia Defensiva de la Torre');

    const noteContentInput = dmPage.locator('#session-note-content-input');
    await noteContentInput.fill('Reforzar las barricadas y colocar arqueros en el campanario.');

    const saveNoteBtn = dmPage.locator('#save-session-note-btn');
    await expect(saveNoteBtn).toBeEnabled();
    await saveNoteBtn.click();

    // La UI indica que la nota fue guardada exitosamente y deshabilita el botón hasta nuevos cambios
    await expect(saveNoteBtn).toContainText('Nota guardada');
    await expect(saveNoteBtn).toBeDisabled();

    // Recargar la página completa con F5 (page.reload())
    await dmPage.reload();

    // Volver a abrir el Modo Partida y la pestaña de notas
    await dmPage.locator('#tab-simulation-btn').click();
    await dmPage.locator('#open-live-session-btn').click();
    await expect(dmPage.locator('#live-session-modal-backdrop')).toBeVisible();

    const dmPartySelectAfterReload = dmPage.locator('#live-session-party-select');
    await dmPartySelectAfterReload.selectOption({ label: 'La Compañía del Bosque' });

    const dmNotesTabAfterReload = dmPage.locator('#tab-session-notes');
    await expect(dmNotesTabAfterReload).toBeVisible();
    await dmNotesTabAfterReload.click();

    // Confirmar que el texto guardado reaparece tras el reload (F5)
    const notesPanelAfterReload = dmPage.locator('#session-notes-editor-panel');
    await expect(notesPanelAfterReload).toContainText('Estrategia Defensiva de la Torre');

    const contentAfterReload = dmPage.locator('#session-note-content-input');
    await expect(contentAfterReload).toHaveValue(
      'Reforzar las barricadas y colocar arqueros en el campanario.'
    );
  });
});
