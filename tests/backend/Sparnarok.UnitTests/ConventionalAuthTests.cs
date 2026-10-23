using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Sparnarok.Api.Domain.Entities;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para autenticación convencional, hashing de contraseñas y vinculación bidireccional.
/// </summary>
public class ConventionalAuthTests
{
    private readonly PasswordHasher<ApplicationUser> _hasher = new();

    [Fact]
    public void CreateConventional_InitializesUserWithPasswordAndNoGoogle()
    {
        var dummy = ApplicationUser.CreateConventional("player@dnd.com", "dummy", "Gandalf");
        var hash = _hasher.HashPassword(dummy, "Secret123!");
        var user = ApplicationUser.CreateConventional("player@dnd.com", hash, "Gandalf");

        user.Email.Should().Be("player@dnd.com");
        user.DisplayName.Should().Be("Gandalf");
        user.PasswordHash.Should().Be(hash);
        user.GoogleSubjectId.Should().BeNull();
        user.HasPassword.Should().BeTrue();
        user.HasGoogleLinked.Should().BeFalse();

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash!, "Secret123!");
        verify.Should().Be(PasswordVerificationResult.Success);
    }

    [Fact]
    public void LinkGoogle_WhenCalledOnConventionalUser_SetsGoogleSubjectIdAndAvatar()
    {
        var dummy = ApplicationUser.CreateConventional("player@dnd.com", "dummy", "Gandalf");
        var hash = _hasher.HashPassword(dummy, "Secret123!");
        var user = ApplicationUser.CreateConventional("player@dnd.com", hash, "Gandalf");

        user.LinkGoogle("google-sub-999", "https://google.com/avatar.png");

        user.HasGoogleLinked.Should().BeTrue();
        user.HasPassword.Should().BeTrue();
        user.GoogleSubjectId.Should().Be("google-sub-999");
        user.AvatarUrl.Should().Be("https://google.com/avatar.png");
    }

    [Fact]
    public void CreateFromGoogle_InitializesUserWithGoogleAndNoPassword()
    {
        var user = ApplicationUser.CreateFromGoogle("google-sub-111", "googleuser@gmail.com", "Valeros", "https://avatar.png");

        user.HasGoogleLinked.Should().BeTrue();
        user.HasPassword.Should().BeFalse();
        user.PasswordHash.Should().BeNull();
        user.GoogleSubjectId.Should().Be("google-sub-111");
    }

    [Fact]
    public void SetPassword_WhenCalledOnGoogleUser_EnablesConventionalLogin()
    {
        var user = ApplicationUser.CreateFromGoogle("google-sub-111", "googleuser@gmail.com", "Valeros");
        var hash = _hasher.HashPassword(user, "NewHeroPass2026!");

        user.SetPassword(hash);

        user.HasPassword.Should().BeTrue();
        user.HasGoogleLinked.Should().BeTrue();
        _hasher.VerifyHashedPassword(user, user.PasswordHash!, "NewHeroPass2026!")
            .Should().Be(PasswordVerificationResult.Success);
    }

    [Fact]
    public void UpdateFromGoogle_WhenCalledWithNewSubject_LinksIfPreviouslyNull()
    {
        var user = ApplicationUser.CreateConventional("merlin@dnd.com", "hash", "Merlin");
        user.GoogleSubjectId.Should().BeNull();

        user.UpdateFromGoogle("Merlin The Wise", "https://pic.png", "sub-merlin-777");

        user.DisplayName.Should().Be("Merlin The Wise");
        user.AvatarUrl.Should().Be("https://pic.png");
        user.GoogleSubjectId.Should().Be("sub-merlin-777");
        user.HasGoogleLinked.Should().BeTrue();
    }
}
