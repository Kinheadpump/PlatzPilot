# 🎓 PlatzPilot ✈️

**PlatzPilot** ist eine smarte, plattformübergreifende Mobile-App (.NET MAUI) für Studierende des Karlsruher Instituts für Technologie (KIT). Sie visualisiert nicht nur die Live-Auslastung aller Bibliotheken und Fakultätsräume in Echtzeit, sondern nutzt historische Daten und mathematische Modelle, um Ankunftszeiten und Mensa-Warteschlangen präzise vorherzusagen.

## ✨ Core Features

* 🔮 **Safe Arrival Prognosen:** Kein Ratespiel mehr. Die App berechnet auf Basis historischer Daten und Wahrscheinlichkeitsverteilungen exakt, bis wann du spätestens an einem Lernplatz sein musst, bevor er voll ist.
* 🍲 **Mensa-Radar:** Ein eigens entwickelter Flux-Algorithmus schätzt die aktuelle Länge der Warteschlange vor der Mensa am Adenauerring, damit du genau weißt, wann der beste Zeitpunkt für die Mittagspause ist.
* 🎛️ **Smarte Filter & Details:** Finde den perfekten Platz! Filtere gezielt nach Gruppenräumen, Ruhebereichen, Whiteboards oder garantierten Steckdosen. Direkte Links zur Platzreservierung sind ebenfalls integriert.
* 📈 **Historische Graphen & "Zeitreise":** Visualisierung der Auslastung der letzten 24 Stunden durch flüssige Spline-Charts. Nutzer können Datum und Uhrzeit verstellen, um Auslastungstrends der letzten Tage zu analysieren.
* 🎨 **Premium UI/UX (Apple-like):** Nativ anfühlendes, minimalistisches Design mit flüssigen Shimmer-Loading-Effekten, schnellem Onboarding und komplettem Support für Light- und Dark-Mode.
* ♿ **Barrierefreiheit & Settings:** Die App passt sich dir an – inklusive Color-Blind-Mode (Farbenblindmodus), anpassbarem haptischen Feedback und der Möglichkeit, geschlossene Räume auszublenden.
* ⭐ **Persönliche Favoriten:** Lieblingsorte lassen sich markieren und werden lokal für einen schnellen Zugriff beim App-Start gespeichert.

## 🛠️ Technologien & Architektur

Dieses Projekt wurde nach strengen **Clean Code** Prinzipien und dem **MVVM-Pattern** (Model-View-ViewModel) entwickelt, um maximale Performance und Wartbarkeit zu garantieren.

* **Framework:** .NET 10 MAUI (Multi-platform App UI)
* **Sprache:** C# 12 & XAML (mit Compiled Bindings für absolute UI-Performance)
* **Pakete:** 
    * `CommunityToolkit.Mvvm` (Source Generators für sauberes Daten-Binding)
    * `Microcharts.Maui` (Für performante 24h-Auslastungsgraphen)
* **Datenquelle:** Öffentliche KIT SeatFinder JSONP-API.
* **Infrastruktur:** *Serverless First* – Alle rechenintensiven Aufgaben (Mensa-Flux-Algorithmus, Beta-Binomial-Wahrscheinlichkeiten für Safe Arrival, Parsing komplexer Arrays) passieren performant und asynchron lokal auf dem Client, um Ladezeiten und Serverkosten zu minimieren.

## 📱 Installation & Ausführen

Die App kann nativ für Android, iOS, Windows und macOS kompiliert werden.

**Voraussetzungen:**
1. [.NET 10 SDK](https://dotnet.microsoft.com/download)
2. Visual Studio 2022 (mit .NET MAUI Workload), JetBrains Rider oder VS Code (mit .NET MAUI Extension)

**Bauen für Android (via Terminal):**
```bash
# Repository klonen
git clone [https://github.com/kinheadpump/PlatzPilot.git](https://github.com/kinheadpump/PlatzPilot.git)
cd PlatzPilot

# Projekt bauen und auf einem angeschlossenen Android-Gerät/Emulator starten
dotnet build -t:Run -c Release -f net10.0-android