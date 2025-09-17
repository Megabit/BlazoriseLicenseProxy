# BlazoriseLicenseProxy

A lightweight **Backend-for-Frontend (BFF)** proxy for **Blazor WebAssembly** apps that need to use **Blazorise**’s product licensing token **without embedding the secret in the client**.  

This project is released under the **MIT License** so Blazorise users can freely adopt and adapt it.

---

## ✨ Why BlazoriseLicenseProxy?

Blazor WebAssembly apps are fully downloaded to the browser, so any secret you include in the bundle (or fetch directly) is visible in DevTools.  

**BlazoriseLicenseProxy** solves this by:

- Keeping your **Blazorise ProductToken** **on the server**.
- Providing a small API endpoint that your apps call instead of contacting the vendor directly.
- Adding **Origin** checks, **custom headers**, and **CORS** restrictions to minimize casual misuse.
- Offering a clean **extension method** for Blazor WASM to fetch the token at startup.

---

## 🏗 Architecture Overview

```
Browser (Blazor WASM)
      │
      ▼
https://api.blazorise.com   ←── Your BlazoriseLicenseProxy (BFF)
      │  (injects ProductToken server-side)
      ▼
Vendor Licensing Service    ←── Secret never leaves server
```

*The browser never receives the vendor token directly, only the BlazoriseLicenseProxy does.*

---

## 🚀 Getting Started

### 1. Clone and Build
```bash
git clone https://github.com/Megabit/BlazoriseLicenseProxy.git
cd BlazoriseLicenseProxy
dotnet restore
```

### 2. Configure Secrets

**appsettings.json**
```json
{
  "AllowedOrigins": [
    "http://localhost:7201",
    "https://bootstrapdemo.blazorise.com",
    "https://bulmademo.blazorise.com"
  ]
}
```

Then set your real ProductToken outside source control:
```bash
dotnet user-secrets init
dotnet user-secrets set "Licensing:ProductToken" "YOUR_REAL_TOKEN"
```

### 3. Run the Proxy

```bash
dotnet run
```
It will start on `https://localhost:7201` by default.

### 3. Have the helper method in your Blazor WASM project

```csharp
/// <summary>
/// Provides a reusable way to fetch the Blazorise product token from a remote API.
/// </summary>
public static class TokenFetch
{
    /// <summary>
    /// Fetches the product token from the licensing API using a GET request with the required header.
    /// </summary>
    /// <param name="apiBase">The base URL of the licensing API.</param>"
    /// <returns>The product token string.</returns>
    public static async Task<string> GetProductTokenAsync( string apiBase )
    {
        using var api = new HttpClient { BaseAddress = new Uri( apiBase ) };

        var req = new HttpRequestMessage( HttpMethod.Get, "/licensing/token" );
        req.Headers.TryAddWithoutValidation( "X-Blazorise-Client", "1" );

        var res = await api.SendAsync( req );
        res.EnsureSuccessStatusCode();

        var payload = await res.Content.ReadFromJsonAsync<TokenResponse>()
            ?? throw new InvalidOperationException( "Failed to read licensing token." );

        return payload.Token;
    }

    private record TokenResponse( string Token );
}
```

### 4. Use in Your Blazor WASM App

Install the **extension** and fetch the token:

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault( args );

var productToken = await TokenFetch.GetProductTokenAsync( builder.HostEnvironment.IsDevelopment()
    ? "https://localhost:7201"
    : "https://api.blazorise.com" ); // Your deployed proxy URL

builder.Services
    .AddBlazorise( options =>
    {
        options.ProductToken = productToken;
    } )
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();

builder.RootComponents.Add<App>("#app");
await builder.Build().RunAsync();
```

---

## 🔒 Security Considerations

- **Secrets are still visible to your server admins**, keep your infrastructure secure.
- Requests and responses **to your proxy** remain visible in DevTools, but **the vendor token never leaves the server**.
- Add **rate limiting**, **logging**, and **monitoring** to detect abuse.
- Rotate your ProductToken periodically.

---

## 📜 License

This project is licensed under the **MIT License**, see [LICENSE](LICENSE) for details.

---

## 🙌 Contributing

1. Fork the repo.
2. Create a new branch (`git checkout -b feature/my-feature`).
3. Commit your changes.
4. Push to your branch (`git push origin feature/my-feature`).
5. Open a Pull Request.

---

## 📣 Acknowledgements

- [Blazorise](https://blazorise.com/) for the UI components and licensing system that inspired this proxy pattern.
- The **Backend-for-Frontend** pattern for guiding the architecture.
