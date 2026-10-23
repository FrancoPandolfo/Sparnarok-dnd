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
/// Pruebas unitarias para SessionNotesController y la entidad SessionNote.
/// Valida la regla de negocio estricta: sólo el Dungeon Master (OwnerUserId de la Party)
/// puede crear, leer, actualizar o eliminar notas de sesión.
/// </summary>
public class SessionNotesTests
{
    private readonly IPartyRepository _partyRepoMock = Substitute.For<IPartyRepository>();
    private readonly ISessionNoteRepository _noteRepoMock = Substitute.For<ISessionNoteRepository>();
    private readonly IValidator<CreateSessionNoteRequest> _createValidator = new CreateSessionNoteRequestValidator();
    private readonly IValidator<UpdateSessionNoteRequest> _updateValidator = new UpdateSessionNoteRequestValidator();

    private readonly Guid _ownerUserId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
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
            new Claim(ClaimTypes.NameIdentifier, callingUserId.ToString())
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
        var note = new SessionNote(Guid.NewGuid(), _partyId, "El Secreto del Archimago", "# Pistas ocultas\nEncontraron un grimorio.");

        // Assert
        note.PartyId.Should().Be(_partyId);
        note.Title.Should().Be("El Secreto del Archimago");
        note.Content.Should().Contain("grimorio");
        note.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));

        // Update
        note.Update("Nuevo Título", "Contenido actualizado");
        note.Title.Should().Be("Nuevo Título");
        note.Content.Should().Be("Contenido actualizado");
        note.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));

        // Invariant: empty title throws
        var actEmptyTitle = () => new SessionNote(Guid.NewGuid(), _partyId, "   ", "Contenido");
        actEmptyTitle.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task GetAll_WhenCallerIsNotPartyOwner_Returns403Forbidden()
    {
        // Arrange
        var party = new Party(_partyId, "La Compañía del Dragón", _ownerUserId);
        _partyRepoMock.GetByIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        var controller = CreateController(_otherUserId); // Not the DM

        // Act
        var result = await controller.GetAll(_partyId, CancellationToken.None);

        // Assert
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetAll_WhenCallerIsPartyOwner_Returns200WithNotes()
    {
        // Arrange
        var party = new Party(_partyId, "La Compañía del Dragón", _ownerUserId);
        _partyRepoMock.GetByIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        var note1 = new SessionNote(Guid.NewGuid(), _partyId, "Nota 1", "Contenido 1");
        var note2 = new SessionNote(Guid.NewGuid(), _partyId, "Nota 2", "Contenido 2");
        _noteRepoMock.GetAllByPartyIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(new List<SessionNote> { note1, note2 });

        var controller = CreateController(_ownerUserId);

        // Act
        var result = await controller.GetAll(_partyId, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtoList = okResult.Value.Should().BeAssignableTo<IReadOnlyList<SessionNoteResponseDto>>().Subject;
        dtoList.Should().HaveCount(2);
        dtoList[0].Title.Should().Be("Nota 1");
    }

    [Fact]
    public async Task Create_WhenCallerIsNotPartyOwner_Returns403Forbidden()
    {
        // Arrange
        var party = new Party(_partyId, "La Compañía del Dragón", _ownerUserId);
        _partyRepoMock.GetByIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        var request = new CreateSessionNoteRequest("Emboscada en el camino", "3 Goblins acechan.");
        var controller = CreateController(_otherUserId);

        // Act
        var result = await controller.Create(_partyId, request, CancellationToken.None);

        // Assert
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Create_WhenCallerIsOwnerAndValid_Returns201Created()
    {
        // Arrange
        var party = new Party(_partyId, "La Compañía del Dragón", _ownerUserId);
        _partyRepoMock.GetByIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        _noteRepoMock.AddAsync(Arg.Any<SessionNote>(), Arg.Any<CancellationToken>())
            .Returns(args => Task.FromResult((SessionNote)args[0]));

        var request = new CreateSessionNoteRequest("Emboscada en el camino", "3 Goblins acechan.");
        var controller = CreateController(_ownerUserId);

        // Act
        var result = await controller.Create(_partyId, request, CancellationToken.None);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = createdResult.Value.Should().BeOfType<SessionNoteResponseDto>().Subject;
        dto.Title.Should().Be("Emboscada en el camino");
        dto.Content.Should().Be("3 Goblins acechan.");
        dto.PartyId.Should().Be(_partyId);
    }

    [Fact]
    public async Task Create_WhenValidationFails_Returns400BadRequest()
    {
        // Arrange
        var party = new Party(_partyId, "La Compañía del Dragón", _ownerUserId);
        _partyRepoMock.GetByIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        var request = new CreateSessionNoteRequest("", "Contenido"); // Title empty
        var controller = CreateController(_ownerUserId);

        // Act
        var result = await controller.Create(_partyId, request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Delete_WhenCallerIsOwner_Returns204NoContent()
    {
        // Arrange
        var party = new Party(_partyId, "La Compañía del Dragón", _ownerUserId);
        _partyRepoMock.GetByIdAsync(_partyId, Arg.Any<CancellationToken>())
            .Returns(party);

        var note = new SessionNote(Guid.NewGuid(), _partyId, "Nota a borrar", "Bye");
        _noteRepoMock.GetByIdAsync(note.Id, Arg.Any<CancellationToken>())
            .Returns(note);
        _noteRepoMock.DeleteAsync(note, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = CreateController(_ownerUserId);

        // Act
        var result = await controller.Delete(_partyId, note.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _noteRepoMock.Received(1).DeleteAsync(note, Arg.Any<CancellationToken>());
    }
}
