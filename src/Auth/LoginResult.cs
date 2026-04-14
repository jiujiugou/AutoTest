namespace Auth;

public record LoginResult(
    string AccessToken,
    string RefreshToken
);