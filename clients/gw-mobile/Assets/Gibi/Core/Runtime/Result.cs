// GW-ARCH-001 section 4 — Gibi.Core result types.
// Failures carry a stable machine-readable code. Player-facing text is resolved in
// Gibi.UI through localization keys; Core never produces prose (section 14).
namespace Gibi.Core
{
    public readonly struct Result<T>
    {
        public readonly bool Success;
        public readonly T Value;
        public readonly string ErrorCode;

        private Result(bool success, T value, string errorCode)
        { Success = success; Value = value; ErrorCode = errorCode; }

        public static Result<T> Ok(T value) => new Result<T>(true, value, null);
        public static Result<T> Fail(string errorCode) => new Result<T>(false, default, errorCode);
    }
}
