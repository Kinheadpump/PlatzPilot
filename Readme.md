# 🎓 PlatzPilot ✈️

**PlatzPilot** ist eine plattformübergreifende Mobile-App (.NET MAUI) für Studierende des Karlsruher Instituts für Technologie (KIT). Sie visualisiert die Live-Auslastungen aller Bibliotheken und Fakultätsräume in Echtzeit.

## 🚀 Features (MVP)

* **Live-Auslastung:** Abruf von Schätzwerten und manuellen Zählungen der KIT-Bibliotheken in Echtzeit.
* **Historische Graphen:** Visualisierung der Auslastung der letzten 24 Stunden durch flüssige Spline-Charts (powered by Microcharts).
* **"Zeitreise":** Nutzer können Datum und Uhrzeit verstellen, um zu schauen wie sich die Auslastung in den letzten Tagen verändert hat.
* **Intelligente Sortierung:** Gebäude lassen sich nach Relevanz, meisten freien Plätzen oder Alphabet sortieren.
* **Persönliche Favoriten:** Lieblingsorte lassen sich markieren und werden lokal auf dem Gerät gespeichert.
* **Modernes UI/UX:** Voller Support für Light- und Dark-Mode, dynamische Farbgebung (Grün, Gelb, Rot) je nach Auslastung und flüssige Navigation.

## 🛠️ Technologien & Architektur

Dieses Projekt wurde nach dem **Clean Architecture** Ansatz und dem **MVVM-Pattern** (Model-View-ViewModel) entwickelt.

* **Framework:** .NET 10 MAUI (Multi-platform App UI)
* **Sprache:** C# & XAML
* **Pakete:** * `CommunityToolkit.Mvvm` (Source Generators für sauberes Daten-Binding)
  * `Microcharts.Maui` (Für die 24h-Auslastungsgraphen)
* **Datenquelle:** Öffentliche KIT SeatFinder JSONP-API. 
* **Infrastruktur:** Serverless First – Alle Berechnungen, Filterungen und Parsings (inklusive komplexer JSON-Arrays und fehlerhafter Datumsformate) passieren lokal auf dem Client, um Serverkosten zu vermeiden.

## 📱 Installation & Ausführen

Die App kann für Android, iOS, Windows und macOS kompiliert werden.

**Voraussetzungen:**
1. [.NET 10 SDK](https://dotnet.microsoft.com/download)
2. Visual Studio 2022 (mit .NET MAUI Workload) oder VS Code (mit .NET MAUI Extension)

**Bauen für Android (via Terminal):**
```bash
# Repository klonen
git clone [https://github.com/kinheadpump/PlatzPilot.git](https://github.com/kinheadpump/PlatzPilot.git)
cd PlatzPilot

# Projekt bauen und auf einem angeschlossenen Android-Gerät/Emulator starten
dotnet build -t:Run -c Release -f net10.0-android
