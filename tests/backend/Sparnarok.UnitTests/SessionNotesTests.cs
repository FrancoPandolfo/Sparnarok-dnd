using System.Security.Claims;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Application.Validators;
using Sparnarok.Api.Controllers;
using Sparnarok.Api.Domain.Entities;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para <see cref="SessionNotesController"/> y la entidad <see cref="SessionNote"/>.
/// Valida la regla de privacidad y aislamiento estricto de la Fase 3.7:
/// Logical Row-Level Security: cada usuario (sea DM o Jugador) sólo puede leer, crear, modificar
/// o eliminar sus propias notas privadas dentro de la misma Party.
/// </summary>
public class SessionNotesTests
{
    private readonly IPartyRepository _partyRepoMock = Substitute.For<IPartyRepository>();
    private readonly ISessionNoteRepository _noteRepoMock = Substitute.For<ISessionNoteRepository>();
    private readonly IValidator<CreateSessionNoteRequest> _createValidator = new CreateSessionNoteRequestValidator();
    private readonly IValidator<UpdateSessionNoteRequest> _updateValidator = new UpdateSessionNoteRequestValidator();

    private readonly Guid _userAId = Guid.NewGuid();
    private readonly Guid _userBId = Guid.NewGuid();
    private readonly Guid _partyId = Guid.NewGuid();

    private SessionNotesController CreateController(Guid callingUserId)
    {
        var controller = new SessionNotesController(
            _partyRepoMock,
            _noteRepoMock,
            _createValidator,
            _updateValidator);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, callingUserId.ToString()),
            new Claim(ClaimTypes.Name, $"User_{callingUserId:N}")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return controller;
    }

    [Fact]
    public void SessionNote_DomainEntity_CreationAndInvariants()
    {
        // Act
        var note = new SessionNote(Guid.NewGuid(), _partyId, _userAId, "El Secreto del Archimago", "# Pistas ocultas\nEncontraron un grimorio.");

        // Assert
        note.PartyId.Should().Be(_partyId);
        note.OwnerUserId.Should().Be(_userAId);
        note.Title.Should().Be("El Secreto del Archimago");
        note.Content.Should().Contain("grimorio");
        note.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        note.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));

        // Update
        note.Update("Nuevo Título", "Contenido actualizado");
        note.Title.Should().Be("Nuevo Título");
        note.Content.Should().Be("Contenido actualizado");
        note.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));

        // Invariant: empty title throws
        var actEmptyTitle = () => new SessionNote(Guid.NewGuid(), _partyId, _userAId, "   ", "Contenido");
        actEmptyTitle.Should().Throw<ArgumentException>();

        // Invariant: empty ownerUserId throws
        var actEmptyOwner = () => new SessionNote(Guid.NewGuid(), _partyId, Guid.Empty, "Título", "Contenido");
        actEmptyOwner.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task GetAll_SimulatingTwoDistinctUsers_UserACannotReceiveUserBNotes()
    {
        // Arrange: Party compartida donde ambos usuarios participan
        var party = new Party(_partyId, "Campaña del Dragón", _userAId);
        _partyRepoMock.GetByIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        var noteOfUserA = new SessionNote(Guid.NewGuid(), _partyId, _userAId, "Nota Secreta del DM", "El rey es un doppelganger.");
        var noteOfUserB = new SessionNote(Guid.NewGuid(), _partyId, _userBId, "Apuntes de Elendur", "Sospecho del tabernero.");

        // El repositorio filtra estrictamente por PartyId y OwnerUserId
        _noteRepoMock.GetAllByPartyAndOwnerAsync(_partyId, _userAId, Arg.Any<CancellationToken>())
            .Returns(new List<SessionNote> { noteOfUserA });

        _noteRepoMock.GetAllByPartyAndOwnerAsync(_partyId, _userBId, Arg.Any<CancellationToken>())
            .Returns(new List<SessionNote> { noteOfUserB });

        var controllerUserA = CreateController(_userAId);
        var controllerUserB = CreateController(_userBId);

        // Act 1: Usuario A consulta las notas de la Party
        var resultA = await controllerUserA.GetAll(_partyId, CancellationToken.None);
        var okResultA = resultA.Result.Should().BeOfType<OkObjectResult>().Subject;
        var notesA = okResultA.Value.Should().BeAssignableTo<IReadOnlyList<SessionNoteResponseDto>>().Subject;

        // Assert 1: Usuario A sólo recibe su propia nota, y jamás la nota de Usuario B
        notesA.Should().HaveCount(1);
        notesA[0].Title.Should().Be("Nota Secreta del DM");
        notesA[0].OwnerUserId.Should().Be(_userAId);
        notesA.Should().NotContain(n => n.Title == "Apuntes de Elendur");

        // Act 2: Usuario B consulta la misma Party
        var resultB = await controllerUserB.GetAll(_partyId, CancellationToken.None);
        var okResultB = resultB.Result.Should().BeOfType<OkObjectResult>().Subject;
        var notesB = okResultB.Value.Should().BeAssignableTo<IReadOnlyList<SessionNoteResponseDto>>().Subject;

        // Assert 2: Usuario B sólo recibe su propia nota, y jamás la nota secreta de Usuario A
        notesB.Should().HaveCount(1);
        notesB[0].Title.Should().Be("Apuntes de Elendur");
        notesB[0].OwnerUserId.Should().Be(_userBId);
        notesB.Should().NotContain(n => n.Title == "Nota Secreta del DM");
    }

    [Fact]
    public async Task GetById_WhenUserAttemptsToReadOtherUserNote_Returns403Forbidden()
    {
        // Arrange: Nota perteneciente a Usuario A
        var party = new Party(_partyId, "Campaña del Dragón", _userAId);
        _partyRepoMock.GetByIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        var noteA = new SessionNote(Guid.NewGuid(), _partyId, _userAId, "Nota Confidencial", "Contenido Secreto");
        _noteRepoMock.GetByIdAsync(noteA.Id, Arg.Any<CancellationToken>())
            .Returns(noteA);

        // Usuario B intenta leer la nota de Usuario A
        var controllerUserB = CreateController(_userBId);

        // Act
        var result = await controllerUserB.GetById(_partyId, noteA.Id, CancellationToken.None);

        // Assert
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Create_WhenCalledByAnyAuthenticatedMember_AssignsOwnerUserIdToCallingUser()
    {
        // Arrange
        var party = new Party(_partyId, "Campaña del Dragón", _userAId);
        _partyRepoMock.GetByIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        _noteRepoMock.AddAsync(Arg.Any<SessionNote>(), Arg.Any<CancellationToken>())
            .Returns(args => Task.FromResult((SessionNote)args[0]));

        var request = new CreateSessionNoteRequest("Mi diario de campaña", "Hoy exploramos las catacumbas.");
        var controllerUserB = CreateController(_userBId); // Usuario B (Jugador)

        // Act
        var result = await controllerUserB.Create(_partyId, request, CancellationToken.None);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = createdResult.Value.Should().BeOfType<SessionNoteResponseDto>().Subject;
        dto.Title.Should().Be("Mi diario de campaña");
        dto.OwnerUserId.Should().Be(_userBId);
        dto.PartyId.Should().Be(_partyId);
    }

    [Fact]
    public async Task Update_WhenUserAttemptsToModifyOtherUserNote_Returns403Forbidden()
    {
        // Arrange: Nota creada por Usuario A
        var party = new Party(_partyId, "Campaña del Dragón", _userAId);
        _partyRepoMock.GetByIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        var noteA = new SessionNote(Guid.NewGuid(), _partyId, _userAId, "Original", "Cuerpo");
        _noteRepoMock.GetByIdAsync(noteA.Id, Arg.Any<CancellationToken>())
            .Returns(noteA);

        var request = new UpdateSessionNoteRequest("Hackeado", "Modificado");
        var controllerUserB = CreateController(_userBId); // Usuario B intenta editar

        // Act
        var result = await controllerUserB.Update(_partyId, noteA.Id, request, CancellationToken.None);

        // Assert
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Delete_WhenUserAttemptsToDeleteOtherUserNote_Returns403Forbidden()
    {
        // Arrange: Nota creada por Usuario A
        var party = new Party(_partyId, "Campaña del Dragón", _userAId);
        _partyRepoMock.GetByIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        var noteA = new SessionNote(Guid.NewGuid(), _partyId, _userAId, "Nota a borrar", "Texto");
        _noteRepoMock.GetByIdAsync(noteA.Id, Arg.Any<CancellationToken>())
            .Returns(noteA);

        var controllerUserB = CreateController(_userBId); // Usuario B intenta borrar

        // Act
        var result = await controllerUserB.Delete(_partyId, noteA.Id, CancellationToken.None);

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        await _noteRepoMock.DidNotReceive().DeleteAsync(Arg.Any<SessionNote>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenOwnerDeletesOwnNote_Returns204NoContent()
    {
        // Arrange
        var party = new Party(_partyId, "Campaña del Dragón", _userAId);
        _partyRepoMock.GetByIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        var noteA = new SessionNote(Guid.NewGuid(), _partyId, _userAId, "Nota propia", "Texto");
        _noteRepoMock.GetByIdAsync(noteA.Id, Arg.Any<CancellationToken>())
            .Returns(noteA);
        _noteRepoMock.DeleteAsync(noteA, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controllerUserA = CreateController(_userAId); // Propietario

        // Act
        var result = await controllerUserA.Delete(_partyId, noteA.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _noteRepoMock.Received(1).DeleteAsync(noteA, Arg.Any<CancellationToken>());
    }
}
