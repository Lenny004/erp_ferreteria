using BCrypt.Net;

var hash = args.Length > 0 ? args[0] : throw new ArgumentException("hash required");
var ok = BCrypt.Verify("1234", hash);
Console.WriteLine(ok ? "PIN_OK" : "PIN_FAIL");
