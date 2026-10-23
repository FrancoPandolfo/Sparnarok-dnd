import { test, expect } from '@playwright/test';

test.describe('Live Combat Refinements Suite (Player Initiative Editing, Combatant Deletion & Bestiary Autocomplete)', () => {
  test('DM opens quick combatant modal, autocompletes monster from bestiary, adds to list, deletes combatant, and Player edits own initiative row', async ({
    browser,
  }) => {
    const partyId = 'party-refinements-e2e';
    const dmUserId = '11111111-1111-1111-1111-111111111111';
    const playerUserId = '22222222-2222-2222-2222-222222222222';

    const partyData = {
      id: partyId,
      name: 'La Compañía Refinada',
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
          inGameName: 'Valeros',
          joinedAt: new Date().toISOString(),
        },
      ],
    };

    const initialCombatState = {
      partyId,
      partyName: 'La Compañía Refinada',
      status: 'WaitingForInitiative',
      currentRound: 1,
      currentTurnIndex: 0,
      combatants: [
        {
          id: 'hero-valeros',
          name: 'Valeros',
          initiative: 12,
          maxHp: 30,
          currentHp: 30,
          armorClass: 16,
          isPlayerCharacter: true,
          isVisibleToPlayers: true,
          assignedPlayerUserId: playerUserId,
          sortOrder: 0,
        },
        {
          id: 'monster-skeleton-1',
          name: 'Esqueleto Guardián',
          initiative: 8,
          maxHp: 15,
          currentHp: 15,
          armorClass: 13,
          isPlayerCharacter: false,
          isVisibleToPlayers: true,
          assignedPlayerUserId: null,
          sortOrder: 1,
        },
      ],
    };

    const mockCreatures = [
      {
        id: 'b0000001-0000-0000-0000-000000000001',
        name: 'Bugbear Stalker',
        size: 'Medium',
        type: 'Humanoid',
        alignment: 'Chaotic Evil',
        challengeRating: 1,
        speedFeet: 30,
        proficiencyBonus: 2,
        effectiveArmorClass: 16,
        abilityScores: {
          strength: 15, strengthModifier: 2,
          dexterity: 14, dexterityModifier: 2,
          constitution: 13, constitutionModifier: 1,
          intelligence: 8, intelligenceModifier: -1,
          wisdom: 11, wisdomModifier: 0,
          charisma: 9, charismaModifier: -1,
        },
        hitPoints: { max: 27, current: 27, temporary: 0 },
        armorClass: { base: 14, shieldBonus: 2, allowsDexterityModifier: false, maxDexterityModifier: null },
        actions: [],
        knownMasteries: [],
        isPlayerCharacter: false,
      },
      {
        id: 'b0000001-0000-0000-0000-000000000099',
        name: 'Goblin Boss',
        size: 'Small',
        type: 'Humanoid',
        alignment: 'Neutral Evil',
        challengeRating: 1,
        speedFeet: 30,
        proficiencyBonus: 2,
        effectiveArmorClass: 17,
        abilityScores: {
          strength: 10, strengthModifier: 0,
          dexterity: 14, dexterityModifier: 2,
          constitution: 10, constitutionModifier: 0,
          intelligence: 10, intelligenceModifier: 0,
          wisdom: 8, wisdomModifier: -1,
          charisma: 10, charismaModifier: 0,
        },
        hitPoints: { max: 21, current: 21, temporary: 0 },
        armorClass: { base: 15, shieldBonus: 2, allowsDexterityModifier: false, maxDexterityModifier: null },
        actions: [],
        knownMasteries: [],
        isPlayerCharacter: false,
      },
    ];

    // -------------------------------------------------------------
    // Context 1: Dungeon Master
    // -------------------------------------------------------------
    const dmContext = await browser.newContext();
    const dmPage = await dmContext.newPage();

    await dmPage.route('**/api/v1/creatures*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockCreatures),
      });
    });

    await dmPage.addInitScript(
      ({ partyData, combatState, dmUserId }) => {
        window.localStorage.setItem(
          'sparnarok_auth',
          JSON.stringify({
            id: dmUserId,
            email: 'dm@refinements.dnd',
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
    // Context 2: Player (Valeros)
    // -------------------------------------------------------------
    const playerContext = await browser.newContext();
    const playerPage = await playerContext.newPage();

    await playerPage.route('**/api/v1/creatures*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockCreatures),
      });
    });

    await playerPage.addInitScript(
      ({ partyData, combatState, playerUserId }) => {
        window.localStorage.setItem(
          'sparnarok_auth',
          JSON.stringify({
            id: playerUserId,
            email: 'valeros@refinements.dnd',
            displayName: 'Valeros Guerrero',
            token: 'jwt-player-token',
          })
        );
        window.localStorage.setItem('sparnarok_parties', JSON.stringify([partyData]));
        window.localStorage.setItem(`sparnarok_combat_${partyData.id}`, JSON.stringify(combatState));
      },
      { partyData, combatState: initialCombatState, playerUserId }
    );

    // Sync bridge between DM and Player contexts for localStorage combat updates
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

    // 1. DM abre la sesión y selecciona la party
    await dmPage.goto('/');
    await dmPage.locator('#tab-simulation-btn').click();
    await dmPage.locator('#open-live-session-btn').click();
    await expect(dmPage.locator('#live-session-modal-backdrop')).toBeVisible();

    const dmPartySelect = dmPage.locator('#live-session-party-select');
    await dmPartySelect.selectOption({ label: 'La Compañía Refinada' });
    await expect(dmPage.locator('#user-party-role-badge')).toContainText('Dungeon Master');

    const dmTable = dmPage.locator('#initiative-order-table');
    await expect(dmTable).toBeVisible();
    await expect(dmTable).toContainText('Valeros');
    await expect(dmTable).toContainText('Esqueleto Guardián');

    // 2. DM abre el modal "+ Añadir Combatiente Rápido"
    const addQuickBtn = dmPage.locator('#add-quick-combatant-btn');
    await expect(addQuickBtn).toBeVisible();
    await addQuickBtn.click();

    const quickModal = dmPage.locator('#quick-combatant-modal');
    await expect(quickModal).toBeVisible();

    // 3. Escribir "Goblin" para buscar en el bestiario Open5e
    const nameInput = dmPage.locator('#quick-combatant-name');
    await nameInput.fill('Goblin');

    // Dropdown con sugerencia "Goblin Boss" debe aparecer
    const suggestionsList = dmPage.locator('#creature-suggestions-list');
    await expect(suggestionsList).toBeVisible();

    const goblinSuggestion = dmPage.locator('#creature-suggestion-goblin-boss');
    await expect(goblinSuggestion).toBeVisible();
    await expect(goblinSuggestion).toContainText('Goblin Boss');
    await expect(goblinSuggestion).toContainText('HP: 21');
    await expect(goblinSuggestion).toContainText('CA: 17');

    // Seleccionar la sugerencia
    await goblinSuggestion.click();

    // Verificar que los campos se autocompletaron correctamente
    await expect(nameInput).toHaveValue('Goblin Boss');
    const hpInput = dmPage.locator('#quick-combatant-hp');
    const acInput = dmPage.locator('#quick-combatant-ac');
    await expect(hpInput).toHaveValue('21');
    await expect(acInput).toHaveValue('17');

    // Modificar HP para validar capacidad custom (sobreescribir a 25)
    await hpInput.fill('25');
    await expect(hpInput).toHaveValue('25');

    // Enviar el formulario
    await dmPage.locator('#quick-combatant-submit-btn').click();
    await expect(quickModal).not.toBeVisible();

    // Verificar que "Goblin Boss" aparece en la tabla del DM
    await expect(dmTable).toContainText('Goblin Boss');

    // 4. Eliminar a "Goblin Boss" usando el botón de papelera
    const goblinRow = dmPage.locator('tr').filter({ hasText: 'Goblin Boss' });
    await expect(goblinRow).toBeVisible();

    const deleteBtn = goblinRow.locator('button[id^="remove-combatant-btn-"]');
    await expect(deleteBtn).toBeVisible();

    // Configurar listener para aceptar la confirmación
    let deleteDialogHandled = false;
    dmPage.once('dialog', async (dialog) => {
      deleteDialogHandled = true;
      expect(dialog.message()).toContain('Goblin Boss');
      await dialog.accept();
    });

    await deleteBtn.click();

    // Confirmar que el diálogo fue interceptado y aceptado
    expect(deleteDialogHandled).toBe(true);

    // Verificar que "Goblin Boss" desapareció de la tabla del DM
    await expect(dmTable).not.toContainText('Goblin Boss');

    // -------------------------------------------------------------
    // 5. Vista del Jugador: Verificar permisos y edición de iniciativa
    // -------------------------------------------------------------
    await playerPage.goto('/');
    await playerPage.locator('#tab-simulation-btn').click();
    await playerPage.locator('#open-live-session-btn').click();
    await expect(playerPage.locator('#live-session-modal-backdrop')).toBeVisible();

    const playerPartySelect = playerPage.locator('#live-session-party-select');
    await playerPartySelect.selectOption({ label: 'La Compañía Refinada' });
    await expect(playerPage.locator('#user-party-role-badge')).toContainText('Jugador');

    const playerTable = playerPage.locator('#initiative-order-table');
    await expect(playerTable).toBeVisible();
    await expect(playerTable).toContainText('Valeros');

    // En la vista de jugador NO debe haber botones de eliminación
    const playerTrashButtons = playerPage.locator('button[id^="remove-combatant-btn-"]');
    await expect(playerTrashButtons).toHaveCount(0);

    // El input de iniciativa para el personaje propio (Valeros) está habilitado
    const ownInitInput = playerPage.locator('#combatant-initiative-input-hero-valeros');
    await expect(ownInitInput).toBeVisible();
    await expect(ownInitInput).toBeEnabled();

    // El input de iniciativa para otro combatiente (Esqueleto Guardián) está deshabilitado
    const otherInitInput = playerPage.locator('#combatant-initiative-input-monster-skeleton-1');
    await expect(otherInitInput).toBeVisible();
    await expect(otherInitInput).toBeDisabled();

    // El jugador actualiza su iniciativa a 20 pulsando Enter
    await ownInitInput.fill('20');
    await ownInitInput.press('Enter');

    // El valor persiste en el input
    await expect(ownInitInput).toHaveValue('20');
  });
});
