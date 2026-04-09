# 🐞 Bug Finder - The Ultimate QA Simulator

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows_Presentation_Foundation-blue?style=for-the-badge)
![MVVM](https://img.shields.io/badge/Architecture-MVVM-success?style=for-the-badge)

## 📝 Foreword: AI Transparency & Development Approach 🤖

*Full disclosure: The majority of the syntax and boilerplate code in this repository was generated using Claude Code.*

Entering this assessment, I had no prior experience with WPF or WinForms. Given the strict time constraints and the goal of delivering a complete, "market-ready product," I made the strategic decision to fully leverage the power of modern AI. My priority was resource optimization—using the best available tools to build the highest quality product possible within the timeframe.

Throughout this development sprint, my role was that of a **Product Architect and Prompt Engineer**. My core contributions included:
* **Product Vision:** Ideating the unique mechanics (Extreme Mode, Quiz Bugs) and the overall UI/UX.
* **System Design:** Dictating the architectural constraints (enforcing the MVVM pattern and strict separation of concerns).
* **Quality Assurance:** Rigorous testing, debugging, and continuous iterative refinement of the prompts to ensure the generated components formed a seamless, performant application.

While the required AI conversation logs are attached in the `docs/ai-assistance/` folder, they do not paint the whole picture. The true effort lay in the critical thinking, decision-making, and constant course correction required to shape raw AI outputs into a cohesive software product.

In today's rapidly evolving tech landscape, I believe the ability to efficiently orchestrate AI tools to deliver tangible business value is a paramount skill. I present this project as a testament to that mindset, and I kindly ask for an objective evaluation based on the product's quality, user experience, and architectural soundness.

## 📌 Product Vision (The "You Build, I Buy" Philosophy)
BugsFinderis a 30-year-old classic, but it lacks the thrill of modern gaming. **Bug Finder** reimagines this classic through the lens of a Software Developer/QA Tester. Instead of sweeping mines, you are hunting down critical bugs before releasing on Production. 

I didn't just rebuild a game; I built a **market-ready product** that introduces action-puzzle mechanics (Extreme Mode), a progression system (Stars & Unlocks), and an educational twist (Quiz Bugs). This is designed not just to be played, but to be highly addictive.

---

## ✨ Core Features & Selling Points

### 🎮 Gameplay Redefined
* **No-Guess Guarantee:** Your first click is ALWAYS safe. The grid is generated *after* your first move to ensure a pure logic puzzle experience.
* **Smart Chording:** Auto-expand empty tiles and click numbers to quick-reveal safe zones.
* **Thematic UI/UX:** Complete with Dark, Light, and Blue themes to match your favorite IDE. 

### ⚡ Extreme Mode (Action-Puzzle Hybrid)
Tired of turn-based logic? Toggle **Extreme Mode** to simulate a real "Production Incident":
* A random ticking time bomb appears every 10-20 seconds depends on the chosen mode.
* You have exactly **5 seconds** to find and flag the critical bug.
* Fail to react, and the system crashes. Succeed, and earn bonus Stars!

### 🎯 Quiz Bugs (Play & Learn)
Finding a bug is good, but understanding it is better. 
* Hidden "Quiz Bugs" will trigger a multiple-choice IT/Logic question when successfully flagged.
* **Correct:** +3 Stars. **Wrong:** -1 Star. 
* This gamifies the learning process, making it perfect for tech assessments or casual learning.

### 🏆 Progression & Retention Mechanics
* **Economy System:** Collect hidden Stars (⭐) across the board or by answering Quiz Bugs.
* **Unlockable Content:** Use Stars to unlock the massive **Hard Mode** or exclusive **Special Maps** (Country-shaped grids).
* **Persistent Leaderboard:** Top 10 fastest times are recorded per difficulty, driving replayability.
* **World Record Bounty:** Beat the real-life BugsFinderworld records embedded in the game to trigger a special animated gift box (Win a simulated 1-year Claude Pro sub!).

---

## 🏗️ Technical Architecture & Best Practices

This application is built with **WPF (Windows Presentation Foundation)** and strictly adheres to enterprise-level software design patterns.

### 1. MVVM Pattern (Model-View-ViewModel)
Powered by `CommunityToolkit.Mvvm`, the UI (`Views`) is completely decoupled from the game rules (`Models`). 
* **Zero Logic in Code-Behind:** `MainWindow.xaml.cs` only handles UI-specific animations (like the Gift Box popup) and layout configurations. All game logic, state management, and timer loops live in `MainViewModel` and `GameEngine`.
* **Data Binding & Commands:** Fluent UI updates without direct control manipulation.

### 2. Service-Oriented Architecture
Dependencies are cleanly separated into dedicated services:
* `GameEngine.cs`: Handles the complex recursive algorithms for board generation, bug placement, and chord revealing.
* `AudioService.cs`: Manages sound effects seamlessly.
* `ThemeService.cs`: Handles runtime ResourceDictionary swapping for Dark/Light mode.
* `QuizService.cs`: Parses and serves the JSON-based quiz questions.

### 3. Asynchronous & Multi-threading
* Utilized `DispatcherTimer` and asynchronous tasks to ensure the UI thread never blocks, even when calculating large 30x16 grids or running the Extreme Mode countdowns concurrently with the main game timer.

---

## 🚀 How to Run the Game

### Prerequisites
* Windows OS
* [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Quick Start
1. Clone the repository.
2. Open `BugsFinder.sln` in Visual Studio 2022.
3. Set `BugsFinder` as the Startup Project.
4. Press `F5` or click **Start** to build and run the application.
* **Directory:** You can review the complete ideation and troubleshooting process in the `docs/ai-assistance/` folder.
* **Core Logic:** The core algorithms (`GameEngine.cs` logic, recursive revealing, MVVM wiring) were carefully reviewed, refactored, and tested by me to ensure structural integrity and absence of memory leaks.

---
*Built with passion for the .NET Developer Technical Assessment.*
