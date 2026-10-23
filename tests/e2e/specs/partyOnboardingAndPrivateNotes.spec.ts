import { test, expect } from '@playwright/test';

test.describe('Fase 3.7: Party Onboarding & Private Session Notes Suite', () => {
  test('User A creates Party as DM ("El Señor Oscuro"), User B joins via token as Player ("Elendur"), and their session notes remain strictly isolated in the DOM', async ({
    browser,
  }) => {
    const inviteToken = 'token-fortaleza-sombria-12345';
    const sharedPartyId = 'party-fortaleza-sombria';

    // -------------------------------------------------------------
    // Context 1: Usuario A (DM - "El Señor Oscuro")
    // -------------------------------------------------------------
    const dmContext = await browser.newContext();
    const dmPage = await dmContext.newPage();

    await dmPage.addInitScript(
      ({ token, partyId }) => {
        window.localStorage.setItem(
          'sparnarok_auth',
          JSON.stringify({
            id: '11111111-1111-1111-1111-111111111111',
            email: 'dm@mordor.dnd',
            displayName: 'Sauron',
            globalNickname: 'El Señor Oscuro',
            token: 'jwt-dm-token',
          })
        );

        // Pre-sembrar la party en almacenamiento local para pruebas E2E deterministas
        const partyData = {
          id: partyId,
          name: 'La Fortaleza Sombría',
          ownerUserId: '11111111-1111-1111-1111-111111111111',
          inviteToken: token,
          createdAt: new Date().toISOString(),
          members: [
            {
              id: 'c-hero-1',
              name: 'Kaelen',
              hitPoints: { max: 30, current: 30 },
              armorClass: { base: 16 },
              isPlayerCharacter: true,
            },
          ],
          partyMembers: [
            {
              id: 'pm-1',
              partyId: partyId,
              userId: '11111111-1111-1111-1111-111111111111',
              role: 'DungeonMaster',
              inGameName: 'El Señor Oscuro',
              joinedAt: new Date().toISOString(),
            },
          ],
        };

        const existingParties = JSON.parse(window.localStorage.getItem('sparnarok_parties') || '[]');
        window.localStorage.setItem('sparnarok_parties', JSON.stringify([partyData, ...existingParties]));
      },
      { token: inviteToken, partyId: sharedPartyId }
    );

    // -------------------------------------------------------------
    // Context 2: Usuario B (Player - "Elendur")
    // -------------------------------------------------------------
    const playerContext = await browser.newContext();
    const playerPage = await playerContext.newPage();

    await playerPage.addInitScript(
      ({ token, partyId }) => {
        window.localStorage.setItem(
          'sparnarok_auth',
          JSON.stringify({
            id: '22222222-2222-2222-2222-222222222222',
            email: 'elendur@gondor.dnd',
            displayName: 'Elendur Telcontar',
            globalNickname: 'Elendur',
            token: 'jwt-player-token',
          })
        );

        const partyData = {
          id: partyId,
          name: 'La Fortaleza Sombría',
          ownerUserId: '11111111-1111-1111-1111-111111111111',
          inviteToken: token,
          createdAt: new Date().toISOString(),
          members: [
            {
              id: 'c-hero-1',
              name: 'Kaelen',
              hitPoints: { max: 30, current: 30 },
              armorClass: { base: 16 },
              isPlayerCharacter: true,
            },
          ],
          partyMembers: [
            {
              id: 'pm-1',
              partyId: partyId,
              userId: '11111111-1111-1111-1111-111111111111',
              role: 'DungeonMaster',
              inGameName: 'El Señor Oscuro',
              joinedAt: new Date().toISOString(),
            },
          ],
        };

        const existingParties = JSON.parse(window.localStorage.getItem('sparnarok_parties') || '[]');
        window.localStorage.setItem('sparnarok_parties', JSON.stringify([partyData, ...existingParties]));
      },
      { token: inviteToken, partyId: sharedPartyId }
    );

    // 1. Navegar Usuario A (DM) a la vista de simulación
    await dmPage.goto('/');
    await dmPage.locator('#tab-simulation-btn').click();

    // Abrir Modo Partida en la vista de Usuario A
    const dmOpenLiveBtn = dmPage.locator('#open-live-session-btn');
    await expect(dmOpenLiveBtn).toBeVisible();
    await dmOpenLiveBtn.click();

    const dmModal = dmPage.locator('#live-session-modal-backdrop');
    await expect(dmModal).toBeVisible();

    // Seleccionar o verificar que estamos en "La Fortaleza Sombría"
    const partySelect = dmPage.locator('#live-session-party-select');
    await partySelect.selectOption({ label: 'La Fortaleza Sombría' });

    // Abrir la pestaña "📜 Mis Notas"
    const dmNotesTab = dmPage.locator('#tab-session-notes');
    await expect(dmNotesTab).toBeVisible();
    await dmNotesTab.click();

    // Crear una nota secreta del DM
    const newNoteBtn = dmPage.locator('#new-session-note-btn');
    await expect(newNoteBtn).toBeVisible();
    await newNoteBtn.click();

    const dmNoteTitleInput = dmPage.locator('#session-note-title-input');
    await dmNoteTitleInput.fill('Planes Secretos del Señor Oscuro');

    const dmNoteContentInput = dmPage.locator('#session-note-content-input');
    await dmNoteContentInput.fill('Emboscada secreta con 4 Espectros en el salón del trono.');

    const dmSaveNoteBtn = dmPage.locator('#save-session-note-btn');
    await dmSaveNoteBtn.click();

    // Verificar que la nota del DM aparece en su lista
    const dmNotesList = dmPage.locator('#session-notes-editor-panel');
    await expect(dmNotesList).toContainText('Planes Secretos del Señor Oscuro');

    // 2. Usuario B (Player) navega con el enlace de invitación (?inviteToken=...)
    await playerPage.goto(`/?inviteToken=${inviteToken}`);

    // Verificar que se abre el modal de onboarding
    const onboardingModal = playerPage.locator('#party-onboarding-modal-backdrop');
    await expect(onboardingModal).toBeVisible();

    // Verificar que se muestra el nombre de la campaña
    const invitedPartyName = playerPage.locator('#invited-party-name');
    await expect(invitedPartyName).toContainText('La Fortaleza Sombría');

    // Seleccionar Rol Player y verificar InGameName
    const roleSelect = playerPage.locator('#onboarding-role-select');
    await roleSelect.selectOption('Player');

    const inGameNameInput = playerPage.locator('#onboarding-ingame-name');
    await inGameNameInput.fill('Elendur');

    // Confirmar incorporación a la Party
    const confirmJoinBtn = playerPage.locator('#confirm-join-party-btn');
    await confirmJoinBtn.click();

    // El modal de onboarding se cierra y el modo partida se abre automáticamente para el jugador
    await expect(onboardingModal).not.toBeVisible();

    const playerLiveModal = playerPage.locator('#live-session-modal-backdrop');
    await expect(playerLiveModal).toBeVisible();

    // El jugador navega a la pestaña "📜 Mis Notas"
    const playerNotesTab = playerPage.locator('#tab-session-notes');
    await expect(playerNotesTab).toBeVisible();
    await playerNotesTab.click();

    const playerNotesList = playerPage.locator('#session-notes-editor-panel');

    // 3. VERIFICACIÓN CRÍTICA DE AISLAMIENTO (Row-Level Security):
    // El Jugador NO DEBE VER la nota del DM ("Planes Secretos del Señor Oscuro")
    await expect(playerNotesList).not.toContainText('Planes Secretos del Señor Oscuro');

    // 4. El Jugador redacta su propia nota personal
    const playerNewNoteBtn = playerPage.locator('#new-session-note-btn');
    await playerNewNoteBtn.click();

    const playerNoteTitleInput = playerPage.locator('#session-note-title-input');
    await playerNoteTitleInput.fill('Diario de Viaje de Elendur');

    const playerNoteContentInput = playerPage.locator('#session-note-content-input');
    await playerNoteContentInput.fill('Sospecho que el Señor Oscuro nos vigila desde las sombras.');

    const playerSaveNoteBtn = playerPage.locator('#save-session-note-btn');
    await playerSaveNoteBtn.click();

    // La nota del jugador debe ser visible en la pantalla del Jugador
    await expect(playerNotesList).toContainText('Diario de Viaje de Elendur');

    // 5. VERIFICACIÓN CRÍTICA EN LA PANTALLA DEL DM:
    // El DM NO DEBE VER la nota del Jugador ("Diario de Viaje de Elendur")
    await expect(dmNotesList).toContainText('Planes Secretos del Señor Oscuro');
    await expect(dmNotesList).not.toContainText('Diario de Viaje de Elendur');

    // Cerrar contextos de navegador
    await dmContext.close();
    await playerContext.close();
  });
});
