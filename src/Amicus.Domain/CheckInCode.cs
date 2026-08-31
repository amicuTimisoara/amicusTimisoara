using System.Security.Cryptography;

namespace Amicus.Domain;

/// <summary>
/// The token behind a booking's QR code.
///
/// Crockford base32 (no I, L, O or U), so it is unambiguous if it ever has to be
/// read aloud or typed in when a camera fails, and it cannot spell anything
/// unfortunate. 10 characters of a 32-symbol alphabet is 50 bits — far beyond
/// guessing at event scale, and the codes carry no meaning anyway.
/// </summary>
public static class CheckInCode
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int Length = 10;

    public static string New()
    {
        var chars = new char[Length];

        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
