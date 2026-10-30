# metroETA

> A native, fast, and modular Hong Kong real-time public transport, map, and live-tile arrival app built for **Windows Phone 8.1 (WinRT)** and **Windows 10 Mobile**.

![Version](https://img.shields.io/badge/version-0.1-0078D7.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20Phone%208.1%20%7C%20W10M-blue.svg)
![Language](https://img.shields.io/badge/language-C%23%20%2F%20XAML-68217A.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

---


## 🗂️ Project Architecture (模組化架構)

The codebase is organized into modular, single-responsibility services under `Bus.WindowsPhone`:

| File | Responsibility |
| :--- | :--- |
| `BusRouteDatabase.cs` | Core coordinator & Franchised Bus engine (KMB, Long Win, Citybus, NLB, Joint KMB/CTB merger, Stop Portal) |
| `MtrBusService.cs` | Dedicated handler for all 23 MTR Bus (`K` routes & `506`) stop sequences, coordinates, and live schedules |
| `GreenMinibusService.cs` | Green Minibus (`GMB`) regional discovery (`HKI`, `KLN`, `NT`), dual-direction expansion, and live ETAs |
| `RailwayService.cs` | MTR Heavy Rail (98 stations), Light Rail (`LRT`) live schedules, and MTR Station-to-Station Fare Calculator |
| `TramAndFerryService.cs` | HK Tramways (12 Eastbound/Westbound routes) & Ferry pier schedules + maritime sea-channel waypoints |
| `GeoMapService.cs` | Haversine GPS distance math, OSRM road geometry, and multi-branch railway track curves (`EAL` / `TKL` spurs) |
| `TileService.cs` | Windows Phone Start Screen Live Tile scheduler, 30s prediction buffer, condition tags, and standby watchdog |
| `TimetableService.cs` | Live GMB API headways and structured Weekday / Saturday / Holiday service-hour timetables |
| `WeatherAndTrafficService.cs` | Hong Kong Observatory (`HKO`) live weather/warnings API and `RTHK` / `TD` live traffic news parser |
| `CacheService.cs` | Fast `LocalFolder` JSON disk cache for offline/instant startup catalog & stop coordinate restoration |
| `ThemeManager.cs` | App settings, Light/Dark theme engine, `StatusBar` styling, Favourites persistence, and localization helpers |

---

## 🛠️ Building & Sideloading (編譯及安裝教學)

### Prerequisites
* **Visual Studio 2013 / 2015** with the **Windows Phone 8.1 SDK** installed.
* A developer-unlocked **Windows Phone 8.1** or **Windows 10 Mobile** device (or Windows Phone 8.1 Emulator).

### Compiling the `.appx` Package
1. Open **`Bus.sln`** in Visual Studio.
2. In the top toolbar, set the build configuration to **`Release`** and target platform to **`ARM`** (for physical phones) or **`x86`** (for the emulator).
3. Double-click **`Package.appxmanifest`** → **Packaging** tab → **Choose Certificate...** → **Configure Certificate...** → **Create test certificate...** to generate a self-signed `.pfx` certificate.
4. Right-click the **`Bus.WindowsPhone`** project in Solution Explorer → **Store** → **Create App Packages...**.
5. Select **"No"** (create packages for sideloading only), check **ARM (Release)**, and click **Create**.

### Installing on Your Phone
* **Via PC USB:** Use **Windows Phone Application Deployment (8.1)** or **WinAppDeployCmd** to deploy the generated `.appx` directly over USB.
* **On-Device (Windows 10 Mobile):** Copy both the `.cer` certificate and the `.appx` package to your phone. Tap the **`.cer`** file first to install the certificate, then tap the **`.appx`** file to install **MetroETA**.

---

## 📜 Credits & Data Attribution (資料來源及版權鳴謝)

* **Public Transport & Traffic Open Data:** Provided by **[DATA.GOV.HK (資料一線通)](https://data.gov.hk/)**, **Transport Department (香港運輸署)**, **KMB / Long Win**, **Citybus**, **New Lantao Bus**, and **MTR Corporation**.
* **Traffic News Bulletins:** Provided by **RTHK (香港電台實時交通消息)** and the **Transport Department**.
* **Map Tiles & Bilingual Labels:** Provided by the **Hong Kong Lands Department (香港地政總署 CSDI GeoData Store API)**.
* **Road Routing Geometry:** Powered by **© OpenStreetMap contributors** and **Project OSRM** under the ODbL license.
* **Weather & Warning Signals:** Provided by the **Hong Kong Observatory (香港天文台開放數據 API)**.

---

## 📄 License

This project is released under the **MIT License**.
