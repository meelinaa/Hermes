# 🔍 C# Klassen-Review & Struktur-Leitfaden

Dieser Leitfaden definiert die verbindlichen Regeln für die **strukturelle Analyse**, **Namensgebung** und das **Klassen-Layout** von C#-Klassen im Projekt `Generix`. Er dient als Arbeitsgrundlage für mehrstufige Code-Reviews und Refactorings – sowohl durch menschliche Reviewer als auch durch KI-Assistenten.

> [!IMPORTANT]
> **Verbindliche Arbeitsgrundlage für Entwickler & KI-Assistenten**  
> Alle hier definierten Regeln sind bei jedem Klassen-Review und bei jeder Neu- oder Umstrukturierung zwingend anzuwenden. Die Analyse erfolgt strikt in den definierten Schritten 1 und 2.

> [!NOTE]
> **Versions-Baseline:** Die MVVM-Beispiele in diesem Guide (`[ObservableProperty] public partial string X { get; set; }`) verwenden die **Partial-Properties-Syntax von CommunityToolkit.Mvvm 8.4+**, die **C# 13 / .NET 9 SDK** voraussetzt. Läuft ein Projekt noch auf .NET 8 / C# 12, ist stattdessen die ältere Feld-Syntax zu verwenden: `[ObservableProperty] private string _x;` (generiert dieselbe `public partial string X { get; set; }`-Property über das Feld). Prüfe das Ziel-Framework, bevor du ein Beispiel 1:1 übernimmst.

---

## 📑 Inhaltsverzeichnis

1. [Übersicht der Review-Schritte](#1-übersicht-der-review-schritte)
2. [Namens-Check (Strikte C# Konventionen & Bad Smells)](#2-namens-check-strikte-c-konventionen--bad-smells)
   - [2.1 Generische Namen](#21-generische-namen)
   - [2.2 Ein-Buchstaben-Variablen](#22-ein-buchstaben-variablen)
   - [2.3 Technische Namen](#23-technische-namen)
   - [2.4 Abkürzungen](#24-abkürzungen)
   - [2.5 Boolean-Naming](#25-boolean-naming)
   - [2.6 Async-Suffix](#26-async-suffix)
   - [2.7 Methoden als Verben, Properties als Nomen](#27-methoden-als-verben-properties-als-nomen)
   - [2.8 Negierte Booleans](#28-negierte-booleans)
   - [2.9 Collection-Naming](#29-collection-naming)
   - [2.10 Command-Naming bei RelayCommand](#210-command-naming-bei-relaycommand)
   - [2.11 Event-Naming](#211-event-naming)
   - [2.12 Tupel & Dictionary Deconstruction](#212-tupel--dictionary-deconstruction)
   - [2.13 Generische Typparameter (T vs. TEntity)](#213-generische-typparameter-t-vs-tentity)
   - [2.14 Architektur-Suffixe (DTOs, ViewModels)](#214-architektur-suffixe-dtos-viewmodels)
   - [2.15 Exception- & Attribut-Suffixe](#215-exception---attribut-suffixe)
   - [2.16 nameof-Zwang](#216-nameof-zwang)
   - [2.17 SRP durch Klassennamen (Verbot von And/Or)](#217-srp-durch-klassennamen-verbot-von-andor)
3. [Klassenstruktur, Layout & Formatierung](#3-klassenstruktur-layout--formatierung)
   - [3.1 Casing Enforcement](#31-casing-enforcement)
   - [3.2 Using-Direktiven-Sortierung](#32-using-direktiven-sortierung)
   - [3.3 Klassenstruktur & Sortierung](#33-klassenstruktur--sortierung)
     - [3.3.1 Die 4 Typen-Blöcke (Sortier-Regel)](#331-die-4-typen-blöcke-sortier-regel)
     - [3.3.2 Reihenfolge der Kategorien](#332-reihenfolge-der-kategorien)
     - [3.3.3 Platzierung von Partial Methods (OnXChanged / OnXChanging)](#333-platzierung-von-partial-methods-onxchanged--onxchanging)
     - [3.3.4 Platzierung von Nested Types](#334-platzierung-von-nested-types)
     - [3.3.5 Platzierung von Static Members](#335-platzierung-von-static-members)
     - [3.3.6 Extension Methods](#336-extension-methods)
     - [3.3.7 Verbot von #region-Blöcken](#337-verbot-von-region-blöcken)
     - [3.3.8 Eine Klasse pro Datei](#338-eine-klasse-pro-datei)
     - [3.3.9 Partial Classes über mehrere Dateien](#339-partial-classes-über-mehrere-dateien)
   - [3.4 Methoden-Parameter-Reihenfolge](#34-methoden-parameter-reihenfolge)
   - [3.5 Primary Constructors](#35-primary-constructors)
   - [3.6 Leerzeilen-Regeln](#36-leerzeilen-regeln)
   - [3.7 Expression-Bodied Members](#37-expression-bodied-members)
   - [3.8 Ternary Operator](#38-ternary-operator)
   - [3.9 Magic Strings & Numbers Extrahierung](#39-magic-strings--numbers-extrahierung)
   - [3.10 XML-Dokumentationskommentare](#310-xml-dokumentationskommentare)
   - [3.11 var-Verwendung (Typinferenz)](#311-var-verwendung-typinferenz)
   - [3.12 sealed-Klassen](#312-sealed-klassen)
   - [3.13 Access Modifier (Sichtbarkeitsregeln)](#313-access-modifier-sichtbarkeitsregeln)
   - [3.14 Namespace- und Ordner-Kongruenz](#314-namespace--und-ordner-kongruenz)
   - [3.15 this.-Verbot](#315-this-verbot)
   - [3.16 Kommentar-Hygiene (Toter Code)](#316-kommentar-hygiene-toter-code)
   - [3.17 Modifier-Reihenfolge](#317-modifier-reihenfolge)
   - [3.18 Vertikaler Parameter-Umbruch (Line Wrapping)](#318-vertikaler-parameter-umbruch-line-wrapping)
   - [3.19 Allman-Klammer-Stil (Brace Placement)](#319-allman-klammer-stil-brace-placement)
   - [3.20 Methoden-Überladungen (Overloads) zusammenhalten](#320-methoden-überladungen-overloads-zusammenhalten)
   - [3.21 Arrow-Anti-Pattern (Nesting-Tiefe)](#321-arrow-anti-pattern-nesting-tiefe)
   - [3.22 Pattern Matching: is null / is not null statt ==/!=](#322-pattern-matching-is-null--is-not-null-statt-)
   - [3.23 Collection-Expressions ([]) statt new List<T>() / new T[]](#323-collection-expressions--statt-new-listt--new-t)
   - [3.24 Records: record vs. record struct vs. class](#324-records-record-vs-record-struct-vs-class)
   - [3.25 Nullable Reference Types, required & init-only Properties](#325-nullable-reference-types-required--init-only-properties)
4. [Schritt 2: Refactoring-Empfehlungen & Zusammenfassung](#4-schritt-2-refactoring-empfehlungen--zusammenfassung)
5. [✅ Klassen-Review Checkliste](#5--klassen-review-checkliste)

---

## 1. Übersicht der Review-Schritte

Das Code-Review einer C#-Klasse erfolgt in **zwei aufeinanderfolgenden, strikt getrennten Schritten**. Jeder Schritt wird vollständig abgeschlossen und mit einer konkreten Empfehlung dokumentiert, bevor der nächste beginnt.

| Schritt | Fokus | Ergebnis |
|---------|-------|----------|
| **Schritt 1** | Strukturelle Analyse, Namensgebung & Klassen-Layout | Alle Bezeichner und die Reihenfolge der Klassenelemente sind konform |
| **Schritt 2** | Refactoring-Empfehlungen & Zusammenfassung | Priorisierte, umsetzbare Verbesserungsvorschläge |

> [!TIP]
> Nach **jedem** Analyseschritt muss eine Empfehlung abgegeben werden, die klar beschreibt, **was** geändert werden muss und **warum**.

---

## 2. Namens-Check (Strikte C# Konventionen & Bad Smells)

*(Teil von Schritt 1: Strukturelle Analyse, Namensgebung & Klassen-Layout)*

Analysiere **jeden einzelnen Bezeichner** (Felder, Properties, Methoden, Parameter, lokale Variablen, Konstanten) im definierten Scope. Identifiziere und korrigiere die folgenden „Smells":

---

### 2.1 Generische Namen

**Regel:** Keine Namen wie `data`, `item`, `obj`, `temp`, `val`, `info`, `manager`, `helper`, `result`, `response`, `value`, `context`. Ersetze sie durch fachlich sprechende Bezeichner.

```csharp
// ❌ Anti-Pattern – Generischer Name ohne fachliche Bedeutung:
var data = await _repository.GetAsync(id);
var item = collection.FirstOrDefault();
var info = user.GetProfileInfo();
var result = await _service.ProcessAsync(input);
var value = slider.GetCurrentPosition();
var temp = DateTime.Now;

// ✅ Pro-Pattern – Fachlich sprechender Name:
var customerOrder = await _repository.GetAsync(id);
var firstTransaction = collection.FirstOrDefault();
var userProfile = user.GetProfileInfo();
var importedProfile = await _service.ProcessAsync(input);
var currentSliderPosition = slider.GetCurrentPosition();
var snapshotTimestamp = DateTime.Now;
```

**Erlaubte Ausnahmen für `result`:** Wenn der Kontext unmissverständlich ist und die Variable nur im nächsten Statement verwendet wird:

```csharp
// ✅ Erlaubt – Unmittelbarer Kontext, sofortige Verwendung:
var result = await _importDownloadProfileUseCase.ExecuteAsync(filePath, dataFormat);
AddImportedDomainTransactions(result.DownloadTransactions);
```

> [!WARNING]
> **Klassennamen-Bad-Smells und ihre Korrekturen:**
>
> | ❌ Bad Smell | ✅ Fachlicher Name |
> |---|---|
> | `DataManager` | `OrderCoordinator`, `ProfileOrchestrator` |
> | `InfoHelper` | `AddressFormatter`, `DateConverter` |
> | `ItemProcessor` | `TransactionValidator`, `InvoiceCalculator` |
> | `Utils` / `Utility` | `PathResolver`, `FormatMapper` |
> | `Handler` (generisch) | `DownloadProgressTracker`, `BatchCancellationHandler` |

---

### 2.2 Ein-Buchstaben-Variablen

**Regel:** Keine Ein-Buchstaben-Variablen wie `x`, `y`, `d`, `s`, `e`, `n`. **Einzige Ausnahme:** `i`, `j`, `k` in klassischen `for`-Schleifen und sehr kurze Lambda-Ausdrücke (z. B. `.Where(x => x.IsActive)` bei trivialem Kontext).

```csharp
// ❌ Anti-Pattern – Unverständliche Einbuchstaben-Variablen:
var d = DateTime.Now - startDate;
var s = user.GetStatus();
var n = transactions.Count;
var e = args.NewItems?.Cast<DownloadTransaction>();
var t = new CancellationTokenSource();
var p = Path.GetExtension(filePath);

// ✅ Pro-Pattern – Ausgeschrieben und sofort verständlich:
var elapsedDuration = DateTime.Now - startDate;
var accountStatus = user.GetStatus();
var transactionCount = transactions.Count;
var addedTransactions = args.NewItems?.Cast<DownloadTransaction>();
var downloadCancellationTokenSource = new CancellationTokenSource();
var fileExtension = Path.GetExtension(filePath);
```

```csharp
// ✅ Erlaubte Ausnahmen:
for (int i = 0; i < items.Count; i++) { /* ... */ }
for (int i = 0; i < rows; i++)
    for (int j = 0; j < columns; j++) { /* ... */ }

var activeUsers = users.Where(u => u.IsActive);         // Triviale Lambda (1 Property)
var names = users.Select(u => u.Name);                   // Triviale Lambda (1 Property)
var sorted = transactions.OrderBy(t => t.CreatedAt);     // Triviale Lambda (1 Property)
```

```csharp
// ❌ Anti-Pattern – Lambda zu komplex für Ein-Buchstaben:
var filtered = transactions.Where(t => t.IsActive && t.DestinationPath != null && t.Progress < 100);

// ✅ Pro-Pattern – Bei komplexen Lambdas ausschreiben:
var incompleteActiveTransactions = transactions.Where(transaction =>
    transaction.IsActive &&
    transaction.DestinationPath is not null &&
    transaction.Progress < 100);
```

---

### 2.3 Technische Namen

**Regel:** Vermeide Typnamen im Bezeichner. Der Name soll den **fachlichen Inhalt** beschreiben, nicht den technischen Typ.

```csharp
// ❌ Anti-Pattern – Technischer Typ im Namen:
List<User> userList;
string stringName;
Dictionary<int, Order> orderDictionary;
bool boolIsActive;
int intCount;
DateTime dateTimeCreated;
ObservableCollection<DownloadTransaction> downloadTransactionObservableCollection;

// ✅ Pro-Pattern – Fachlicher Inhalt ohne Typ-Redundanz:
List<User> users;
string name;
Dictionary<int, Order> ordersByCustomerId;
bool isActive;
int retryCount;
DateTime createdAt;
ObservableCollection<DownloadTransaction> downloadTransactions;
```

```csharp
// ❌ Anti-Pattern – Redundanter Typ im Rückgabewert:
public string GetNameString() => _name;
public List<User> GetUserList() => _users.ToList();
public bool CheckIsActiveBool() => _isActive;

// ✅ Pro-Pattern – Fachlich klar ohne Typ-Suffix:
public string GetName() => _name;
public List<User> GetUsers() => _users.ToList();
public bool CheckIsActive() => _isActive;
```

---

### 2.4 Abkürzungen

**Regel:** Keine Vokale sparen! Ausgeschriebene, vollständige Wörter sind immer vorzuziehen. Abkürzungen erzwingen mentale Entschlüsselung und verlangsamen das Lesen.

```csharp
// ❌ Anti-Pattern – Vokale verschluckt, kryptische Abkürzungen:
var usr = GetCurrentUser();
var cust = _customerRepo.Find(id);
var addr = customer.GetAddress();
var idx = items.IndexOf(target);
var btn = FindButton("submit");
var msg = "Operation fehlgeschlagen";
var dlg = new ConfirmationDialog();
var cfg = LoadConfiguration();
var tx = transactionViewModel.ToEntity();
var src = downloadTransaction.SourceUrl;
var dest = downloadTransaction.DestinationPath;
var ext = Path.GetExtension(filePath);
var prof = _downloadProfileRepository.GetById(id);
var fmt = _formatMapperFactory.MapToDataFileFormat(input);

// ✅ Pro-Pattern – Vollständig ausgeschrieben:
var currentUser = GetCurrentUser();
var customer = _customerRepository.Find(id);
var shippingAddress = customer.GetAddress();
var targetIndex = items.IndexOf(target);
var submitButton = FindButton("submit");
var errorMessage = "Operation fehlgeschlagen";
var confirmationDialog = new ConfirmationDialog();
var applicationConfiguration = LoadConfiguration();
var transactionEntity = transactionViewModel.ToEntity();
var sourceUrl = downloadTransaction.SourceUrl;
var destinationPath = downloadTransaction.DestinationPath;
var fileExtension = Path.GetExtension(filePath);
var downloadProfile = _downloadProfileRepository.GetById(id);
var dataFormat = _formatMapperFactory.MapToDataFileFormat(input);
```

> [!NOTE]
> **Branchenübliche Akronyme** wie `URL`, `HTML`, `HTTP`, `API`, `DTO`, `SQL`, `IO` sind erlaubt und müssen nicht ausgeschrieben werden. Ebenso sind etablierte Projekt-Abkürzungen (z. B. `Csv`, `Xlsx`, `Json`, `Xml`) zulässig, sofern sie im gesamten Projekt einheitlich verwendet werden.

**Erlaubte Abkürzungen im Projekt `Generix`:**

| Abkürzung | Bedeutung | Kontext |
|-----------|-----------|---------|
| `Csv` | Comma-Separated Values | Dateiformat-Enum |
| `Xlsx` | Excel-Dateiformat | Dateiformat-Enum |
| `Json` | JavaScript Object Notation | Dateiformat-Enum |
| `Xml` | Extensible Markup Language | Dateiformat-Enum |
| `DTO` | Data Transfer Object | Architektur-Taxonomie |
| `UI` | User Interface | Layer-Bezeichnung |
| `VM` / `ViewModel` | View Model | MVVM-Pattern |
| `Cts` | CancellationTokenSource | Nur als lokale Variable (`var cts = new CancellationTokenSource()`) |

---

### 2.5 Boolean-Naming

**Regel:** Booleans müssen immer mit einem **Zustandspräfix** beginnen, der eine Ja/Nein-Frage formuliert. Erlaubte Präfixe:

| Präfix | Verwendung | Beispiel |
|--------|-----------|----------|
| `Is` | Zustand / Eigenschaft | `IsLoading`, `IsActive`, `IsVisible` |
| `Has` | Besitz / Vorhandensein | `HasPermission`, `HasUnsavedChanges` |
| `Can` | Fähigkeit / Berechtigung | `CanDownload`, `CanEdit`, `CanDelete` |
| `Should` | Empfehlung / Bedingung | `ShouldRetry`, `ShouldShowWarning` |
| `Was` | Vergangener Zustand | `WasModified`, `WasCancelled` |
| `Are` | Plural-Zustand | `AreAllSelected`, `AreTransactionsLoaded` |

```csharp
// ❌ Anti-Pattern – Boolean ohne Zustandspräfix:
private bool _loading;
private bool _enabled;
private bool _downloading;
public bool Active { get; set; }
public bool Expanded { get; set; }
public bool BatchDownloading { get; set; }
bool valid = CheckInput();
bool empty = collection.Count == 0;

// ✅ Pro-Pattern – Boolean mit Zustandspräfix:
private bool _isLoading;
private bool _isEnabled;
private bool _isDownloading;
public bool IsActive { get; set; }
public bool IsExpanded { get; set; }
public bool IsBatchDownloading { get; set; }
bool isValid = CheckInput();
bool isEmpty = collection.Count == 0;
```

```csharp
// ✅ Pro-Pattern – Booleans in realistischen Szenarien:
public bool IsExpanderOpen { get; set; }
public bool HasUnsavedChanges => _originalData != CurrentData;
public bool CanStartBatchDownload => !IsBatchDownloading && ProfileDownloadTransactions.Count > 0;
public bool ShouldShowEmptyState => !IsLoading && FilteredDownloadTransactionCount == 0;
public bool WasLastExportSuccessful { get; private set; }
public bool AreAllTransactionsCompleted => ProfileDownloadTransactions.All(t => t.IsCompleted);
```

> [!WARNING]
> Booleans, die nur ein Adjektiv oder Partizip ohne Präfix verwenden (z. B. `Visible`, `Open`, `Selected`), sind **verboten**. Sie müssen immer die Form `IsVisible`, `IsOpen`, `IsSelected` haben.

**Boolean-Naming in Methoden-Rückgaben:**

```csharp
// ❌ Anti-Pattern – Methoden mit bool-Rückgabe ohne klaren Namen:
public bool Check(string input) { /* ... */ }
public bool Process(Order order) { /* ... */ }

// ✅ Pro-Pattern – Methoden mit bool-Rückgabe als Frage formuliert:
public bool IsValidEmail(string input) { /* ... */ }
public bool CanProcessOrder(Order order) { /* ... */ }
public bool HasSufficientBalance(decimal amount) { /* ... */ }
public bool TryParseProfileFormat(string input, out DataFileFormat format) { /* ... */ }
```

---

### 2.6 Async-Suffix

**Regel:** Jede Methode, die `async` ist oder ein `Task` / `Task<T>` / `ValueTask<T>` zurückgibt, **muss** mit dem Suffix `Async` enden. Dies gilt für alle Sichtbarkeiten (`public`, `private`, `internal`, `protected`).

```csharp
// ❌ Anti-Pattern – Async-Methode ohne Suffix:
public async Task LoadData() { /* ... */ }
private async Task<User?> GetCurrentUser(int id) { /* ... */ }
public Task SaveProfile() { /* ... */ }
private async Task ImportTransactions(string path) { /* ... */ }
public async ValueTask<int> CountActive() { /* ... */ }

// ✅ Pro-Pattern – Async-Suffix vorhanden:
public async Task LoadDataAsync() { /* ... */ }
private async Task<User?> GetCurrentUserAsync(int id) { /* ... */ }
public Task SaveProfileAsync() { /* ... */ }
private async Task ImportTransactionsAsync(string path) { /* ... */ }
public async ValueTask<int> CountActiveAsync() { /* ... */ }
```

> [!IMPORTANT]
> **Ausnahme bei `[RelayCommand]`:** Wenn CommunityToolkit MVVM aus einer Methode `ImportProfileAsync()` automatisch ein `ImportProfileCommand` generiert, ist der `Async`-Suffix an der Methode **erwünscht und korrekt**. Das generierte Command trägt bewusst kein `Async` im Namen.

```csharp
// ✅ Pro-Pattern – RelayCommand mit Async-Suffix:
[RelayCommand]
public async Task ImportProfileAsync() { /* ... */ }
// → Generiert: ImportProfileCommand (ohne "Async" im Command-Namen)

[RelayCommand]
public async Task ExportProfileAsync(object? commandParameter) { /* ... */ }
// → Generiert: ExportProfileCommand

[RelayCommand]
public async Task LoadDownloadTransactionsAsync() { /* ... */ }
// → Generiert: LoadDownloadTransactionsCommand
```

**Async-Suffix bei Interface-Methoden:**

```csharp
// ❌ Anti-Pattern – Interface ohne Async-Suffix:
public interface IDownloadProfileRepository
{
    Task<DownloadProfile?> GetById(int id);
    Task Save(DownloadProfile profile);
}

// ✅ Pro-Pattern – Interface mit Async-Suffix:
public interface IDownloadProfileRepository
{
    Task<DownloadProfile?> GetByIdAsync(int id);
    Task SaveAsync(DownloadProfile profile);
}
```

---

### 2.7 Methoden als Verben, Properties als Nomen

**Regel:** Methoden beschreiben **Aktionen** und beginnen deshalb immer mit einem **Verb**. Properties beschreiben **Zustände oder Daten** und verwenden **Nomen oder Adjektive** (ggf. mit Zustandspräfix bei Booleans).

**Erlaubte Verb-Präfixe für Methoden:**

| Verb | Bedeutung | Beispiel |
|------|-----------|----------|
| `Get` | Daten abrufen | `GetUserById()` |
| `Set` | Wert setzen | `SetDefaultPath()` |
| `Create` | Neues Objekt erzeugen | `CreateTransaction()` |
| `Delete` / `Remove` | Löschen / Entfernen | `DeleteProfile()`, `RemoveTransaction()` |
| `Update` | Aktualisieren | `UpdateDownloadStatus()` |
| `Add` | Hinzufügen | `AddTransaction()` |
| `Load` | Laden (oft async) | `LoadProfileAsync()` |
| `Save` | Speichern | `SaveChangesAsync()` |
| `Validate` | Prüfen / Validieren | `ValidateInput()` |
| `Calculate` | Berechnen | `CalculateTotal()` |
| `Parse` | Text zerlegen | `ParseCsvLine()` |
| `Map` / `Convert` / `To` | Umwandeln | `MapToDto()`, `ToEntity()` |
| `Try` | Versuch (bool-Rückgabe) | `TryParse()`, `TryGetValue()` |
| `Initialize` / `Setup` | Initialisieren | `InitializeComponents()` |
| `Dispose` | Ressourcen freigeben | `Dispose()` |
| `Cancel` | Abbrechen | `CancelDownload()` |
| `Export` / `Import` | Daten exportieren / importieren | `ExportProfileAsync()` |
| `Apply` | Anwenden | `ApplyFilter()` |
| `Resolve` | Auflösen | `ResolveDestinationPath()` |
| `Build` | Zusammenbauen | `BuildDownloadRequest()` |
| `Register` / `Unregister` | An/Abmelden | `RegisterEventHandlers()` |
| `Show` / `Hide` | Anzeigen / Verstecken | `ShowToast()`, `HideOverlay()` |
| `Start` / `Stop` | Starten / Stoppen | `StartBatchDownloadAsync()` |
| `Open` / `Close` | Öffnen / Schließen | `OpenFileAsync()`, `CloseConnection()` |
| `Enable` / `Disable` | Aktivieren / Deaktivieren | `EnableAutoRefresh()` |

```csharp
// ❌ Anti-Pattern – Methode ohne Verb / Property wie eine Aktion:
public string UserName() => _user.Name;            // Methode liest sich wie Property
public string GetDownloadProfileName { get; }      // Property liest sich wie Methode
public void TransactionDeletion(int id) { }        // Nomen statt Verb
public DownloadProfile ProfileCreation() { }       // Nomen statt Verb

// ✅ Pro-Pattern – Klare Trennung:
public string GetUserName() => _user.Name;          // Methode mit Verb
public string DownloadProfileName { get; }           // Property als Nomen
public void DeleteTransaction(int id) { }            // Verb als Methodenname
public DownloadProfile CreateProfile() { }           // Verb als Methodenname
```

```csharp
// ✅ Pro-Pattern – Realistische Beispiele aus einem ViewModel:
// Properties (Nomen / Zustand):
public string BatchProgress { get; set; }
public string DownloadProfileName { get; set; }
public int FilteredDownloadTransactionCount => ProfileDownloadTransactions?.Count ?? 0;
public string? DownloadProfileFileName { get; init; }

// Methoden (Verben / Aktionen):
public async Task LoadDownloadTransactionsAsync() { /* ... */ }
public void DeleteTransaction(object? commandParameter) { /* ... */ }
private string ResolveDestinationPath(string path, string fileName, string sourceUrl) { /* ... */ }
private void AddImportedDomainTransactions(IList<DownloadTransaction> transactions) { /* ... */ }
private ViewModelXDownloadTransaction CreateWiredTransactionViewModel(DownloadTransaction source) { /* ... */ }
```

---

### 2.8 Negierte Booleans

**Regel:** Vermeide doppelte Negationen und negierte Boolean-Namen. Sie erzwingen mentales Umkehren und führen zu schwer lesbarem Code.

```csharp
// ❌ Anti-Pattern – Doppelte Negation / Negierter Name:
if (!isNotActive) { /* ... */ }              // Was bedeutet "nicht nicht aktiv"?
private bool _isNotVisible;                  // Negierter Name
if (!hasNoPermission) { /* ... */ }          // Doppelte Verneinung
bool isDisabled = !IsEnabled;                // Negierte Ableitung als Feld
if (!isNotDownloading && !hasNoConnection)   // Unlesbarer Ausdruck

// ✅ Pro-Pattern – Positive, klare Benennung:
if (isActive) { /* ... */ }                  // Sofort verständlich
private bool _isVisible;                     // Positiver Name
if (hasPermission) { /* ... */ }             // Eindeutig
bool isEnabled = IsEnabled;                  // Positiv formuliert
if (isDownloading && hasConnection)           // Klar und lesbar
```

```csharp
// ❌ Anti-Pattern – Negierter Name in realistischem Szenario:
private bool _isNotExpanderOpen;
public bool HasNoTransactions => ProfileDownloadTransactions.Count == 0;
if (!isNotBatchDownloading) { StartBatch(); }

// ✅ Pro-Pattern – Positive Formulierung:
private bool _isExpanderOpen;
public bool HasTransactions => ProfileDownloadTransactions.Count > 0;
// Oder für den leeren Zustand: public bool IsEmpty => ProfileDownloadTransactions.Count == 0;
if (IsBatchDownloading) { StartBatch(); }
```

> [!TIP]
> **Faustregel:** Wenn ein `!`-Operator nötig ist, um den Normalfall auszudrücken, ist der Name falsch gewählt. Der **positive Zustand** sollte der Standardname sein.

---

### 2.9 Collection-Naming

**Regel:** Collections werden in der **Pluralform** des fachlichen Inhalts benannt. Vermeide technische Suffixe wie `List`, `Collection`, `Array`, `Dictionary` im Namen (außer der Typ ist fachlich relevant).

```csharp
// ❌ Anti-Pattern – Technischer Suffix im Collection-Namen:
List<User> userList;
ObservableCollection<DownloadTransaction> transactionCollection;
Dictionary<int, Order> orderDictionary;
string[] nameArray;
List<ViewModelXDownloadTransaction> viewModelList;
HashSet<string> urlHashSet;

// ✅ Pro-Pattern – Pluralform ohne technischen Suffix:
List<User> users;
ObservableCollection<DownloadTransaction> downloadTransactions;
Dictionary<int, Order> ordersByCustomerId;       // Qualifiziert, da Key relevant
string[] customerNames;
List<ViewModelXDownloadTransaction> transactionViewModels;
HashSet<string> processedUrls;
```

```csharp
// ✅ Ausnahme – Fachlich qualifizierende Suffixe sind erlaubt:
Dictionary<string, DataFileFormat> formatLookup;       // "Lookup" ist fachlich
FrozenDictionary<string, string> extensionMapping;     // "Mapping" ist fachlich
ImmutableArray<string> supportedExtensions;            // Plural reicht
ConcurrentQueue<DownloadRequest> pendingDownloads;     // Plural + fachlich
```

```csharp
// ✅ Pro-Pattern – Collection-Naming in realistischem ViewModel:
private ObservableCollection<ViewModelXDownloadTransaction> ProfileDownloadTransactions { get; set; } = [];
private readonly List<DownloadTransaction> _importedTransactions = [];
private readonly FrozenDictionary<string, DataFileFormat> _formatLookup;
```

---

### 2.10 Command-Naming bei RelayCommand

**Regel:** CommunityToolkit MVVM generiert aus der Methodenbezeichnung automatisch ein `Command`-Property. Die Methode selbst folgt den normalen Namenskonventionen (Verb + Kontext). Das Toolkit hängt `Command` automatisch an.

| Methode | Generiertes Command |
|---------|---------------------|
| `DeleteTransaction()` | `DeleteTransactionCommand` |
| `ImportProfileAsync()` | `ImportProfileCommand` |
| `CancelBatchDownload()` | `CancelBatchDownloadCommand` |
| `AddNewDownloadTransaction()` | `AddNewDownloadTransactionCommand` |
| `ChooseDownloadPath()` | `ChooseDownloadPathCommand` |

```csharp
// ❌ Anti-Pattern – "Command" redundant im Methodennamen:
[RelayCommand]
public void DeleteTransactionCommand() { /* ... */ }
// → Generiert: DeleteTransactionCommandCommand (doppelt!)

// ❌ Anti-Pattern – Generischer Name ohne fachlichen Kontext:
[RelayCommand]
public void Execute() { /* ... */ }
// → Generiert: ExecuteCommand (nichtssagend)

// ❌ Anti-Pattern – Unpassendes Nomen statt Verb:
[RelayCommand]
public void TransactionDeletion() { /* ... */ }
// → Generiert: TransactionDeletionCommand (kein Verb)

// ✅ Pro-Pattern – Fachlich sprechend, Verb + Kontext:
[RelayCommand]
public void DeleteTransaction(object? commandParameter) { /* ... */ }
// → Generiert: DeleteTransactionCommand

[RelayCommand]
public async Task ExportProfileAsync(object? commandParameter) { /* ... */ }
// → Generiert: ExportProfileCommand

[RelayCommand]
public async Task LoadDownloadTransactionsAsync() { /* ... */ }
// → Generiert: LoadDownloadTransactionsCommand

[RelayCommand]
private void AddNewDownloadTransaction() { /* ... */ }
// → Generiert: AddNewDownloadTransactionCommand
```

---

### 2.11 Event-Naming

**Regel:** Events verwenden **Partizip-Formen** (Vergangenheit oder Verlaufsform) und beschreiben den **Zustand**, nicht die Aktion. Kein `On`-Präfix im Event-Namen selbst – das `On` gehört nur in die **auslösende Methode**.

| Zeitform | Verwendung | Beispiel |
|----------|-----------|----------|
| Verlaufsform (`...ing`) | Event **vor** der Aktion (pre-event, abbruchfähig) | `ProfileDeleting`, `TransactionChanging` |
| Vergangenheit (`...ed`) | Event **nach** der Aktion (post-event, informativ) | `ProfileDeleted`, `TransactionChanged` |

```csharp
// ❌ Anti-Pattern – Event mit "On"-Präfix oder als Verb:
public event EventHandler? OnDelete;              // "On" gehört nicht in den Event-Namen
public event EventHandler? DeleteProfile;         // Verb statt Partizip
public event EventHandler? ProfileDeletion;       // Nomen statt Partizip

// ✅ Pro-Pattern – Korrekte Partizip-Form:
public event EventHandler? DeleteDownloadProfileRequested;    // "Requested" = Partizip
public event EventHandler? DownloadCompleted;                 // Vergangenheit
public event EventHandler? BatchProgressChanged;              // Vergangenheit
public event EventHandler<CancelEventArgs>? ProfileDeleting;  // Verlaufsform (abbruchfähig)
```

```csharp
// ✅ Pro-Pattern – Auslösende Methode mit "On"-Präfix:
public event EventHandler? DeleteDownloadProfileRequested;

private void OnDeleteDownloadProfileRequested()
{
    DeleteDownloadProfileRequested?.Invoke(this, EventArgs.Empty);
}
```

---

### 2.12 Tupel & Dictionary Deconstruction

**Regel:** Verwende bei Dictionaries niemals `kvp.Key` und `kvp.Value`. Verwende bei Tupeln niemals `Item1` und `Item2`. Nutze **immer** Deconstruction mit sprechenden Namen.

```csharp
// ❌ Anti-Pattern – Generische Dictionary/Tuple Namen:
foreach (var kvp in ordersByCustomerId)
{
    var id = kvp.Key;
    var order = kvp.Value;
}
var result = GetResult();
if (result.Item1) { Console.WriteLine(result.Item2); }

// ✅ Pro-Pattern – Deconstruction mit fachlichen Namen:
foreach (var (customerId, customerOrder) in ordersByCustomerId)
{
    // ...
}
var (isSuccess, errorMessage) = GetResult();
if (isSuccess) { Console.WriteLine(errorMessage); }
```

---

### 2.13 Generische Typparameter (T vs. TEntity)

**Regel:** Wenn eine Klasse oder Methode nur **einen** generischen Parameter hat und der Kontext trivial ist, reicht `T` (z.B. `List<T>`). Sobald es **mehr als einen** gibt oder die Rolle des Typs unklar ist, MUSS ein fachlicher Name mit dem Präfix `T` verwendet werden.

```csharp
// ❌ Anti-Pattern – Unklare generische Parameter:
public interface IMapper<T1, T2> { T2 Map(T1 input); }
public class Dictionary<T, U> { /* ... */ }

// ✅ Pro-Pattern – Sprechende generische Parameter:
public interface IMapper<TSource, TDestination> { TDestination Map(TSource input); }
public class Dictionary<TKey, TValue> { /* ... */ }
public interface IRepository<TEntity> { /* ... */ }
```

---

### 2.14 Architektur-Suffixe (DTOs, ViewModels)

**Regel:** Klassen, die Daten über architektonische Grenzen hinweg transportieren, benötigen ein Suffix zur Abgrenzung von der reinen Domain-Entität.

| Typ | Suffix | Beispiel |
|-----|--------|----------|
| API Request | `Request` | `CreateProfileRequest` |
| API Response | `Response` | `ProfileResponse` |
| Data Transfer Object | `Dto` | `UserDto` |
| View Model (UI) | `ViewModel` | `DownloadProfileViewModel` |
| Domain Model | *(kein Suffix)* | `DownloadProfile`, `User` |

```csharp
// ❌ Anti-Pattern – Keine Abgrenzung der Schichten:
public User Create(User user) { /* ... */ } // Ist das der DTO, Request oder DB-Entity?

// ✅ Pro-Pattern – Klare Schichten durch Suffixe:
public UserResponse Create(CreateUserRequest request) { /* ... */ }
```

---

### 2.15 Exception- & Attribut-Suffixe

**Regel:** Eigene Exception-Klassen müssen zwingend mit `Exception` enden. Eigene Attribut-Klassen müssen zwingend mit `Attribute` enden.

```csharp
// ❌ Anti-Pattern – Fehlende Suffixe:
public class UserNotFound : Exception { }
public class RequireAuth : Attribute { }

// ✅ Pro-Pattern – Zwingende Suffixe:
public class UserNotFoundException : Exception { }
public class RequireAuthAttribute : Attribute { }
```


---

### 2.16 nameof-Zwang

**Regel:** Eigenschaftsnamen, Methodennamen oder Klassennamen dürfen **niemals** als hartcodierte Strings (Magic Strings) geschrieben werden (z. B. für `INotifyPropertyChanged`, Exception-Meldungen oder Reflection). Nutze zwingend den `nameof`-Operator.

```csharp
// ❌ Anti-Pattern – Magic String für Property-Name:
OnPropertyChanged("IsLoading");
throw new ArgumentNullException("user");

// ✅ Pro-Pattern – Typsicher durch nameof:
OnPropertyChanged(nameof(IsLoading));
throw new ArgumentNullException(nameof(user));
```

---

### 2.17 SRP durch Klassennamen (Verbot von And/Or)

**Regel:** Bindewörter wie `And` oder `Or` sind in Klassennamen strikt verboten. Ein Name wie `XAndY` ist ein untrügliches Zeichen dafür, dass das *Single Responsibility Principle* (SRP) verletzt wurde.

```csharp
// ❌ Anti-Pattern – Klasse macht offensichtlich zu viel:
public class DownloadAndUploadCoordinator { }
public class FileParserOrSaver { }

// ✅ Pro-Pattern – Aufgeteilt in fokussierte Klassen:
public class DownloadCoordinator { }
public class UploadCoordinator { }
```

---

## 3. Klassenstruktur, Layout & Formatierung

*(Teil von Schritt 1: Strukturelle Analyse, Namensgebung & Klassen-Layout)*

Dieser Abschnitt prüft **Casing, Datei-Organisation und die Reihenfolge aller Klassenelemente** gegen die vorgegebene Struktur.

---

### 3.1 Casing Enforcement

Erzwinge konsistentes Casing gemäß den C#-Konventionen. Jeder Verstoß muss korrigiert werden.

| Element | Casing | Beispiel |
|---------|--------|----------|
| `public` / `protected` / `internal` Properties | PascalCase | `public string DownloadProfileName { get; set; }` |
| `public` / `protected` / `internal` Methoden | PascalCase | `public async Task LoadDataAsync()` |
| `private` Felder | _camelCase | `private readonly IToastService _toastService;` |
| Parameter | camelCase | `void Process(string filePath)` |
| Lokale Variablen | camelCase | `var downloadTransaction = GetTransaction();` |
| Konstanten | PascalCase oder UPPER_CASE | `private const string DefaultProfileName = "...";` |
| Interfaces | `I` + PascalCase | `IDownloadProfileRepository` |
| Enums & Enum-Werte | PascalCase | `DataFileFormat.Csv` |
| Type Parameters | `T` + PascalCase | `TEntity`, `TResult` (oder einzelnes `T`) |
| Events | PascalCase (Partizip) | `DownloadCompleted`, `ProfileDeleted` |

> [!IMPORTANT]
> **Preservation-Ausnahme für Event-Handler:** Event-Handler-Methoden dürfen das Muster `[Element]_[Event]` beibehalten (z. B. `SubmitButton_Click`, `FilteredTransactions_CollectionChanged`). Abgesehen von dieser einen Ausnahme ist `snake_case` in Bezeichnern **absolut verboten**.

```csharp
// ❌ Anti-Pattern – Inkonsistentes Casing:
public string download_profile_name { get; set; }    // snake_case in Property
private IToastService ToastService;                   // PascalCase für private Feld
void process_data(string File_Path) { }               // snake_case überall
private const string DEFAULT_NAME = "test";           // UPPER_CASE bei Konstanten (projektabhängig)
public interface downloadProfileRepository { }        // Kein I-Prefix, kein PascalCase

// ✅ Pro-Pattern – Korrektes Casing:
public string DownloadProfileName { get; set; }       // PascalCase für public Property
private readonly IToastService _toastService;         // _camelCase für private Feld
void ProcessData(string filePath) { }                 // PascalCase Methode, camelCase Parameter
private const string DefaultName = "test";            // PascalCase für Konstanten
public interface IDownloadProfileRepository { }       // I-Prefix + PascalCase
```

```csharp
// ✅ Erlaubte Ausnahme – Event-Handler Preservation:
private void FilteredTransactions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
{
    // [Element]_[Event]-Muster ist hier erlaubt
}

private void ExpanderToggle_Click(object sender, RoutedEventArgs e)
{
    // [Element]_[Event]-Muster ist hier erlaubt
}
```

---

### 3.2 Using-Direktiven-Sortierung

**Regel:** Using-Direktiven werden in **zwei Gruppen** sortiert, getrennt durch eine Leerzeile. Innerhalb jeder Gruppe wird **alphabetisch** sortiert (A–Z).

| Reihenfolge | Gruppe | Beispiel |
|-------------|--------|----------|
| 1 | **Framework/System-Namespaces & Drittanbieter** | `System`, `System.Collections.Generic`, `CommunityToolkit.Mvvm` |
| 2 | **Projekt-Namespaces** | `Generix.Core.Application`, `Generix.Desktop.WinUI3` |

```csharp
// ❌ Anti-Pattern – Unsortiert, keine Gruppentrennung:
using Generix.Core.Application.ObjectArchetypes.DTOs;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using Generix.Desktop.WinUI3.BehavioralComponents.Factory;
using System.Linq;
using Generix.Core.Application.Ports.Inbound.UseCase.Handler.Download;

// ✅ Pro-Pattern – Gruppiert und alphabetisch sortiert:
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Generix.Core.Application.ObjectArchetypes.DTOs;
using Generix.Core.Application.ObjectArchetypes.Enums;
using Generix.Core.Application.Ports.Inbound.UseCase.Handler.Download;
using Generix.Desktop.WinUI3.BehavioralComponents.Factory;
using Generix.Desktop.WinUI3.BehavioralComponents.Utility;
using Generix.Desktop.WinUI3.Services.Toast.Interface;
```

> [!NOTE]
> **File-scoped Namespaces** (`namespace Generix.Core.Application;`) werden in diesem Projekt bevorzugt gegenüber Block-scoped Namespaces (`namespace Generix.Core.Application { }`), sofern die Datei nur einen Namespace enthält.

---

### 3.3 Klassenstruktur & Sortierung

Bringe die Elemente der Klasse in die exakt definierte logische Reihenfolge. **Verwende KEINE `#region`-Blöcke!**

---

#### 3.3.1 Die 4 Typen-Blöcke (Sortier-Regel)

Diese Regel gilt **strikt** für Felder, Observable Properties und reguläre Properties. Gruppiere die Elemente in die folgenden **4 Blöcke** (exakt in dieser Reihenfolge) und trenne die Blöcke **zwingend durch eine Leerzeile**. Sortiere **innerhalb** jedes Blocks alphabetisch (A–Z).

> [!CAUTION]
> Das Schlüsselwort `readonly` bestimmt **NICHT** den Block! Eine `readonly ObservableCollection<T>` gehört in **Block 4** (Komplexe Typen), nicht in Block 1 (Injizierte Abhängigkeiten).

| Block | Inhalt | Beispiele |
|-------|--------|-----------|
| **Block 1** | **Injizierte Abhängigkeiten (Dependencies)** – Nur Interfaces und Services, die von außen injiziert werden | `private readonly ILocalizationService _localizationService;`<br>`private readonly DialogManager _dialogManager;` |
| **Block 2** | **Primitive Typen & Strings** – Einfache Werttypen und Zeichenketten | `private bool _isLoading;`<br>`private int _retryCount;`<br>`private string _searchText;` |
| **Block 3** | **Enums** – Aufzählungstypen | `private BrowserType _selectedBrowser;`<br>`private DownloadPathOption _pathOption;` |
| **Block 4** | **Komplexe Typen, Collections & UI-Elemente** – Alles, was nicht in Block 1–3 fällt | `private CancellationTokenSource _cancellationTokenSource;`<br>`private readonly ObservableCollection<T> _items;`<br>`private DispatcherQueue _dispatcherQueue;` |

**Vollständiges Beispiel der 4-Block-Sortierung für private Felder:**

```csharp
// ── Block 1: Injizierte Abhängigkeiten (alphabetisch) ──
private readonly ICustomerRepository _customerRepository;
private readonly DialogChannelService _dialogChannelManager;
private readonly IFilePickerService _filePickerService;
private readonly IToastNotificationHandler _toastService;
private readonly IViewContext _viewContext;

// ── Block 2: Primitive Typen & Strings (alphabetisch) ──
private bool _isLoading;
private int _profileId;
private string _downloadProfileNameBuffer;

// ── Block 3: Enums (alphabetisch) ──
private DataFileFormat _activeFormat;
private DownloadPathOption _pathOption;

// ── Block 4: Komplexe Typen, Collections & UI-Elemente (alphabetisch) ──
private CancellationTokenSource _cancellationTokenSource = new();
private readonly IDownloadProfileFileFormatMapperFactory _formatMapperFactory;
private readonly ObservableCollection<DownloadTransaction> _transactions;
```

---

#### 3.3.2 Reihenfolge der Kategorien

Die Elemente einer Klasse werden in die folgenden **9 Kategorien** sortiert, exakt in dieser Reihenfolge von oben nach unten. **Jede Kategorie wendet intern die 4 Typen-Blöcke an** (sofern zutreffend).

| Nr. | Kategorie | Beschreibung | 4-Block-Sortierung? |
|-----|-----------|--------------|---------------------|
| 1 | **Constants** | Konstanten (`const`, `static readonly`) | ✅ Ja |
| 2 | **Fields** | Private Felder **ohne** `[ObservableProperty]` | ✅ Ja |
| 3 | **Observable Properties** | Felder oder `partial` Properties **mit** `[ObservableProperty]`, gefolgt von ihren zugehörigen Partial Methods (`OnXChanged`, `OnXChanging`) *(nur bei MVVM)* | ✅ Ja |
| 4 | **Properties** | Explizite `get; set;` Eigenschaften **ohne** MVVM-Attribute | ✅ Ja |
| 5 | **Events & Delegates** | `event`-Deklarationen und Delegate-Felder | ❌ Nein (alphabetisch) |
| 6 | **Constructors** | Konstruktoren | ❌ Nein |
| 7 | **Commands** | Methoden **mit** `[RelayCommand]` *(nur bei MVVM)* | ❌ Nein (alphabetisch) |
| 8 | **Methods** | Restliche Methoden: Zuerst `public`, dann `private` | ❌ Nein |
| 9 | **Nested Types** | Innere Klassen, Records, Enums, Structs | ❌ Nein |

Das vollständige Beispiel ist in Abschnitt [3.3.3](#333-platzierung-von-partial-methods-onxchanged--onxchanging) unten dargestellt.

---

#### 3.3.3 Platzierung von Partial Methods (OnXChanged / OnXChanging)

**Regel:** Die vom CommunityToolkit MVVM generierten `partial void OnXChanging()` und `partial void OnXChanged()` Callback-Methoden gehören **direkt unter** die zugehörige `[ObservableProperty]`-Deklaration, **nicht** in die allgemeine Methods-Kategorie.

```csharp
// ❌ Anti-Pattern – Partial Method weit entfernt von der zugehörigen Property:
[ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
[ObservableProperty] public partial bool IsBatchDownloading { get; set; }

// ... 200 Zeilen weiter unten ...
partial void OnSearchTextChanged(string value) { /* ... */ }

// ✅ Pro-Pattern – Partial Method direkt unter der zugehörigen Property:
[ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

partial void OnSearchTextChanged(string value)
{
    DownloadTransactionsView?.Refresh();
}

[ObservableProperty] public partial bool IsBatchDownloading { get; set; }
```

> [!TIP]
> Wenn eine `[ObservableProperty]` **keine** zugehörige Partial Method hat, kann sie einzeilig bleiben und direkt neben anderen einzeiligen Observable Properties stehen. Erst wenn eine Partial Method folgt, wird eine Leerzeile vor der Property eingefügt, um den Block visuell abzutrennen.

**Vollständiges Beispiel der gesamten Klassenstruktur:**

```csharp
public sealed partial class ViewModelExample(
    ICustomerRepository customerRepository,
    IToastNotificationHandler toastService) : ObservableObject, IDisposable
{
    // ═══════════════════════════════════════════════════════
    //  1. Constants
    // ═══════════════════════════════════════════════════════
    private const string DefaultProfileName = "Unnamed Profile";
    private const int MaxRetryAttempts = 3;

    // ═══════════════════════════════════════════════════════
    //  2. Fields
    // ═══════════════════════════════════════════════════════
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IToastNotificationHandler _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));

    private bool _isLoading;
    private string _searchBuffer = string.Empty;

    private CancellationTokenSource _cancellationTokenSource = new();

    // ═══════════════════════════════════════════════════════
    //  3. Observable Properties (+ Partial Methods)
    // ═══════════════════════════════════════════════════════
    [ObservableProperty] public partial string BatchProgress { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBatchDownloading { get; set; }

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        DownloadTransactionsView?.Refresh();
    }

    [ObservableProperty] public partial AdvancedCollectionView? DownloadTransactionsView { get; set; }
    [ObservableProperty] public partial DownloadProfile? DownloadProfile { get; set; }

    [ObservableProperty] private partial ObservableCollection<ViewModelXDownloadTransaction> ProfileDownloadTransactions { get; set; } = [];

    partial void OnProfileDownloadTransactionsChanging(ObservableCollection<ViewModelXDownloadTransaction> value)
    {
        if (ProfileDownloadTransactions is not null)
            ProfileDownloadTransactions.CollectionChanged -= FilteredTransactions_CollectionChanged;
    }

    partial void OnProfileDownloadTransactionsChanged(ObservableCollection<ViewModelXDownloadTransaction> value)
    {
        if (ProfileDownloadTransactions is not null)
            ProfileDownloadTransactions.CollectionChanged += FilteredTransactions_CollectionChanged;
        OnPropertyChanged(nameof(FilteredDownloadTransactionCount));
    }

    // ═══════════════════════════════════════════════════════
    //  4. Properties
    // ═══════════════════════════════════════════════════════
    public string? DownloadProfileFileName { get; init; }
    public int FilteredDownloadTransactionCount => ProfileDownloadTransactions?.Count ?? 0;

    public bool IsExpanderOpen { get; set; }

    // ═══════════════════════════════════════════════════════
    //  5. Events & Delegates
    // ═══════════════════════════════════════════════════════
    public event EventHandler? DeleteDownloadProfileRequested;

    // ═══════════════════════════════════════════════════════
    //  6. Constructors (bei Primary Constructor oft leer)
    // ═══════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════
    //  7. Commands
    // ═══════════════════════════════════════════════════════
    [RelayCommand]
    public async Task DeleteTransactionAsync(object? commandParameter) { /* ... */ }

    [RelayCommand]
    public async Task ExportProfileAsync(object? commandParameter) { /* ... */ }

    // ═══════════════════════════════════════════════════════
    //  8. Methods (public → private)
    // ═══════════════════════════════════════════════════════
    public void Dispose() { /* ... */ }

    private string ResolveDestinationPath(string path) { /* ... */ }

    private static void IgnoreProgress(int completed, int total) => _ = (completed, total);

    // ═══════════════════════════════════════════════════════
    //  9. Nested Types
    // ═══════════════════════════════════════════════════════
    private sealed record DownloadProgressSnapshot(int Completed, int Total);
}
```

> [!NOTE]
> Die Kommentar-Trennlinien (z. B. `// ═══════`) dienen hier nur der Illustration. Im produktiven Code sind sie **optional**.

---

#### 3.3.4 Platzierung von Nested Types

**Regel:** Innere Klassen, Records, Structs und Enums stehen **am Ende** der Klasse (Kategorie 9), nach allen Methoden.

```csharp
public sealed class DownloadService
{
    // ... Kategorien 1–8 ...

    // ── 9. Nested Types ──
    private sealed record DownloadResult(bool IsSuccess, string? ErrorMessage);

    private enum DownloadState
    {
        Idle,
        InProgress,
        Completed,
        Failed
    }
}
```

> [!WARNING]
> Innere Typen sollten die **Ausnahme** sein, nicht die Regel. Wenn ein Nested Type mehr als ~30 Zeilen hat oder wiederverwendbar ist, extrahiere ihn in eine eigene Datei.

---

#### 3.3.5 Platzierung von Static Members

**Regel:** Statische Members werden **nicht** in einen eigenen Block separiert, sondern innerhalb ihrer jeweiligen Kategorie einsortiert. Innerhalb einer Kategorie stehen `static`-Members **vor** Instanz-Members.

```csharp
// ── 1. Constants ──
private const string DefaultName = "Unnamed";                             // const (implizit static)
private static readonly FrozenDictionary<string, string> FormatLookup;    // static readonly

// ── 8. Methods ──
// public static vor public instance:
public static DataFileFormat ParseFormat(string extension) { /* ... */ }
public void LoadProfile() { /* ... */ }

// private static vor private instance:
private static void IgnoreProgress(int completed, int total) => _ = (completed, total);
private string ResolveDestinationPath(string path) { /* ... */ }
```

---

#### 3.3.6 Extension Methods

**Regel:** Erweiterungsmethoden haben strikte C#-Konventionen für Struktur und Namensgebung:
1. Die Klasse muss `static` sein.
2. Der Klassenname muss mit `Extensions` enden (z.B. `StringExtensions`).
3. Sie darf keinen internen Zustand (Felder) halten.
4. Der erste Parameter muss mit `this` modifiziert sein.

```csharp
// ❌ Anti-Pattern – Falscher Name, keine statische Klasse:
public class StringHelper 
{
    public static bool IsValid(this string input) { /* ... */ }
}

// ✅ Pro-Pattern – Korrekte Extension-Struktur:
public static class StringExtensions
{
    public static bool IsValid(this string input) { /* ... */ }
}
```


---

#### 3.3.7 Verbot von #region-Blöcken

**Regel:** Die Verwendung von `#region` und `#endregion` ist im gesamten Projekt **strikt verboten**. 

**Begründung:** Regions werden fast ausschließlich dazu missbraucht, zu große Klassen (God Objects) oder chaotischen Code zu verstecken. Wenn eine Klasse die definierten 9 Kategorien (Block 1–4) konsequent anwendet, ist sie visuell perfekt strukturiert. Wird sie dennoch zu unübersichtlich, verletzt sie das Single Responsibility Principle (SRP) und muss in mehrere Klassen aufgeteilt werden.

```csharp
// ❌ Anti-Pattern – Verstecken von Code in Regions:
#region Services
private readonly IUserService _userService;
private readonly IEmailService _emailService;
#endregion

#region Methods
public void DoSomething() { /* ... */ }
#endregion

// ✅ Pro-Pattern – Klare Struktur durch Kategorien (ohne Regions):
private readonly IEmailService _emailService;
private readonly IUserService _userService;

public void DoSomething() { /* ... */ }
```


---

#### 3.3.8 Eine Klasse pro Datei

**Regel:** Eine Datei darf **exakt eine** öffentliche Klasse, ein Interface oder ein Enum enthalten. (Ausnahme: Kleine private Nested Types, wie in 3.3.4 definiert). Zudem muss der Dateiname auf das Zeichen genau dem Typnamen entsprechen.

```text
// ❌ Anti-Pattern – Sammeldatei oder falscher Dateiname:
Datei: UserInterfaces.cs
Inhalt: public interface IUserService { } 
        public interface IEmailService { }

// ✅ Pro-Pattern – 1:1 Mapping:
Datei: IUserService.cs
Inhalt: public interface IUserService { }
```

---

#### 3.3.9 Partial Classes über mehrere Dateien

**Regel:** Bei `partial class` (z. B. WinUI-Code-Behind mit generiertem XAML-Teil) gelten die 9 Kategorien und 4 Typen-Blöcke **pro physischer Datei getrennt**, nicht über die gesamte logische Klasse hinweg. Jede Datei-Teilklasse sortiert nur die Members, die sie selbst deklariert – es werden keine Members aus anderen Partial-Dateien "mitsortiert" oder dorthin verschoben.

```csharp
// ✅ Pro-Pattern – MainPage.xaml.cs (UI-Interaktion, vom Framework generierter Teil):
public sealed partial class MainPage : Page
{
    // Kategorien 1-9 NUR für Members, die in dieser Datei deklariert sind
    private readonly INavigationService _navigationService;

    public MainPage()
    {
        InitializeComponent();
    }

    private void SubmitButton_Click(object sender, RoutedEventArgs e) { /* ... */ }
}

// ✅ Pro-Pattern – MainPage.ViewModelBinding.cs (fachliche Logik, separat gepflegt):
public sealed partial class MainPage
{
    // Eigene, unabhängige 9-Kategorien-Sortierung für DIESE Datei
    private void RefreshBindings() { /* ... */ }
}
```

> [!TIP]
> **Faustregel für die Aufteilung:** Wenn eine `partial class` aus fachlichen Gründen auf mehrere Dateien verteilt wird (nicht nur wegen Framework-Codegenerierung wie XAML), muss jede Datei einen **erkennbaren, benannten Verantwortungsbereich** haben (z. B. `MainPage.xaml.cs`, `MainPage.DragAndDrop.cs`). Eine `partial class` ohne klaren Aufteilungsgrund ist meist ein SRP-Verstoß (siehe 2.17) und sollte stattdessen in eigenständige, komponierte Klassen aufgelöst werden.

---

### 3.4 Methoden-Parameter-Reihenfolge

**Regel:** Methodenparameter werden in einer **festen Reihenfolge** angeordnet:

| Reihenfolge | Parameterart | Beispiel |
|-------------|-------------|----------|
| 1 | **Pflicht-Parameter** (fachlich primär) | `string filePath`, `int profileId` |
| 2 | **Optionale Parameter** (mit Defaultwert) | `int maxRetries = 3`, `string? label = null` |
| 3 | **Callback-Delegates** | `Action<int, int>? onProgress = null` |
| 4 | **`CancellationToken`** (immer letzter Parameter) | `CancellationToken cancellationToken = default` |

```csharp
// ❌ Anti-Pattern – CancellationToken nicht am Ende:
public async Task DownloadAsync(
    CancellationToken token,
    Action<int>? onProgress,
    string sourceUrl,
    string destinationPath,
    int maxRetries = 3) { /* ... */ }

// ✅ Pro-Pattern – Korrekte Reihenfolge:
public async Task DownloadAsync(
    string sourceUrl,
    string destinationPath,
    int maxRetries = 3,
    Action<int>? onProgress = null,
    CancellationToken cancellationToken = default) { /* ... */ }
```

```csharp
// ✅ Pro-Pattern – Realistisches Beispiel:
public async Task ExecuteAsync(
    StartBatchDownloadRequest request,
    Action<int, int>? onBatchProgress = null,
    Action<DownloadTransaction, DownloadProgressReport>? onItemProgress = null,
    Action<DownloadTransaction, Exception>? onError = null,
    CancellationToken cancellationToken = default) { /* ... */ }
```

---

### 3.5 Primary Constructors

**Regel:** Bei Verwendung von C# 12+ Primary Constructors gelten folgende Konventionen:

1. **Parameter des Primary Constructors** folgen `camelCase`.
2. **Sofortige Zuweisung** in `private readonly`-Felder mit `_camelCase` im Klassenkörper bei Validierung oder Mehrfachverwendung.
3. **Keine direkte Nutzung** von Primary-Constructor-Parametern tief im Klassenkörper.

```csharp
// ❌ Anti-Pattern – Primary-Constructor-Parameter direkt überall verwendet:
public sealed partial class ViewModelProfile(
    IToastNotificationHandler toastService,
    IFilePickerService filePickerService,
    string downloadProfileName) : ObservableObject
{
    [RelayCommand]
    public void Save()
    {
        toastService.ShowToast($"Profil '{downloadProfileName}' gespeichert.");
    }
}

// ✅ Pro-Pattern – Sofortige Zuweisung in benannte Felder mit Validierung:
public sealed partial class ViewModelProfile(
    IToastNotificationHandler toastService,
    IFilePickerService filePickerService,
    string downloadProfileName) : ObservableObject
{
    private readonly IFilePickerService _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
    private readonly IToastNotificationHandler _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));

    private readonly string _downloadProfileNameBuffer = downloadProfileName;

    [RelayCommand]
    public void Save()
    {
        _toastService.ShowToast($"Profil '{_downloadProfileNameBuffer}' gespeichert.");
    }
}
```

> [!IMPORTANT]
> **Ausnahme:** Primary-Constructor-Parameter, die **ausschließlich** für die Initialisierung von `[ObservableProperty]`-Werten verwendet werden, dürfen direkt referenziert werden:

```csharp
// ✅ Erlaubt – Einmalige Verwendung im Property-Initializer:
public sealed partial class ViewModelProfile(string downloadProfileName) : ObservableObject
{
    [ObservableProperty] public partial string DownloadProfileName { get; set; } = downloadProfileName;
}
```

**Wann Primary Constructor, wann klassischer Konstruktor?**

| Situation | Primary Constructor | Klassischer Konstruktor |
|-----------|:--------------------:|:------------------------:|
| Reine DI-Injection ohne Validierungslogik | ✅ Bevorzugt | Unnötig verbose |
| Validierung über `ArgumentNullException.ThrowIfNull` hinaus (z. B. Wertebereichs-Checks) | ⚠️ Möglich, aber Logik landet in Feld-Initializern – unübersichtlich | ✅ Bevorzugt (Konstruktor-Body für Logik) |
| Mehrere Konstruktor-Überladungen nötig | ❌ Nicht möglich (nur ein Primary Constructor) | ✅ Erforderlich |
| Klasse mit `[ObservableProperty]`-Initialisierung aus Konstruktor-Parametern | ✅ Bevorzugt (siehe Ausnahme oben) | Unnötig verbose |
| Basisklassen-Aufruf mit Berechnung (`base(ComputeSomething(x))`) | ⚠️ Nur bei trivialer Weiterleitung sinnvoll | ✅ Bevorzugt bei komplexer Logik |

> [!TIP]
> **Faustregel:** Primary Constructor ist der **Standard** für DI-Klassen (Services, ViewModels, Adapter) ohne Validierungslogik jenseits von Null-Checks. Sobald der Konstruktor-Body mehr tut als Zuweisen und `ThrowIfNull`, wechsle zum klassischen Konstruktor – ein Primary Constructor mit einer versteckten Logik-Kaskade in Feld-Initializern ist schwerer zu lesen als ein expliziter Konstruktor-Body.

---

### 3.6 Leerzeilen-Regeln

Konsistente Leerzeilen verbessern die visuelle Struktur und Lesbarkeit massiv.

#### Zwischen Klassenmembern

| Situation | Leerzeilen |
|-----------|-----------|
| Zwischen **unterschiedlichen Kategorien** (z. B. Fields → Observable Properties) | **2 Leerzeilen** |
| Zwischen **den 4 Typen-Blöcken** innerhalb einer Kategorie | **1 Leerzeile** |
| Zwischen **einzeiligen Members** im selben Block | **Keine Leerzeile** |
| Zwischen einem **einzeiligen Member** und einer **Partial Method** | **1 Leerzeile** vor der Property mit Partial Method |
| Zwischen **Methoden** (Commands, Methods) | **1 Leerzeile** |

#### Innerhalb von Methoden

| Situation | Leerzeilen |
|-----------|-----------|
| Nach Guard Clauses / Early Returns (als Block) | **1 Leerzeile** nach dem letzten Guard |
| Vor `return`-Statements am Ende einer Methode | **1 Leerzeile** (wenn vorher logischer Block) |
| Zwischen logisch zusammengehörigen Anweisungsgruppen | **1 Leerzeile** |
| Zwischen einzelnen Anweisungen derselben logischen Gruppe | **Keine Leerzeile** |
| Nach Variablendeklarationen vor ihrer Verwendung | **1 Leerzeile** (wenn mehr als 2 Deklarationen) |

```csharp
// ✅ Pro-Pattern – Leerzeilen strukturieren die Methode:
public async Task ImportProfileAsync()
{
    var profileFilePath = await _filePickerService.PickFileAsync();

    if (string.IsNullOrWhiteSpace(profileFilePath))
    {
        _toastService.ShowToast("Keine Datei ausgewählt.");
        return;
    }

    var fileExtension = Path.GetExtension(profileFilePath).TrimStart('.');

    if (string.IsNullOrWhiteSpace(fileExtension))
    {
        _toastService.ShowToast("Ungültiges Dateiformat.");
        return;
    }

    var dataFormat = _formatMapperFactory.MapToDataFileFormat(fileExtension);

    try
    {
        await ProcessImportAsync(profileFilePath, dataFormat);
    }
    catch (IOException ioException)
    {
        Debug.WriteLine($"Dateizugriffsfehler: {ioException.Message}");
    }
}
```

---

### 3.7 Expression-Bodied Members

**Regel:** Verwende Expression-Bodied Members (`=>`) für **einzeilige** Ausdrücke. Verwende Block-Bodies (`{ }`) für **mehrzeilige** Logik oder Methoden mit **Seiteneffekten**.

| Situation | Empfehlung | Beispiel |
|-----------|-----------|----------|
| Read-only Property (Berechnung) | `=>` | `public int Count => _items.Count;` |
| Einfache Methode (1 Ausdruck) | `=>` | `public string GetName() => _name;` |
| Methode mit Seiteneffekten | `{ }` | `public void Save() { _repo.Store(_data); }` |
| Mehrzeilige Logik | `{ }` | Methoden mit if/else, try/catch, Schleifen |
| `void`-Methoden mit 1 Aufruf | `=>` erlaubt | `public void Notify() => _service.Send();` |

```csharp
// ✅ Pro-Pattern – Expression-Bodied (einzeilig, seiteneffektfrei):
public int FilteredCount => ProfileDownloadTransactions?.Count ?? 0;
public string DisplayName => $"{FirstName} {LastName}";
public string? MapToExtension(string? input) => ExtensionLookup.GetValueOrDefault(input ?? string.Empty);
private static void IgnoreProgress(int completed, int total) => _ = (completed, total);
public override string ToString() => $"Profile: {DownloadProfileName}";

// ✅ Pro-Pattern – Block-Body (mehrzeilig oder Seiteneffekte):
public async Task SaveAsync()
{
    ArgumentNullException.ThrowIfNull(DownloadProfile);
    await _repository.UpdateAsync(DownloadProfile);
    _toastService.ShowToast("Profil gespeichert.");
}
```

> [!TIP]
> **Faustregel:** Wenn ein Expression-Body länger als ~100 Zeichen wird oder einen Zeilenumbruch erzwingt, sollte er in einen Block-Body umgewandelt werden.

---

### 3.8 Ternary Operator

**Regel:** Der ternäre Operator (`? :`) darf **ausschließlich** für einzeilige Werte-Zuweisungen genutzt werden. Er darf **niemals verschachtelt** werden (`a ? b : c ? d : e`). Sobald ein Zeilenumbruch nötig wäre, muss ein `if/else` oder eine `switch`-Expression genutzt werden.

```csharp
// ❌ Anti-Pattern – Verschachtelter oder unübersichtlicher Ternary:
var status = isRunning ? "Running" : hasError ? "Error" : isPending ? "Pending" : "Unknown";

var result = (condition1 && condition2) || condition3 
    ? ComputeComplexResultA() 
    : ComputeComplexResultB();

// ✅ Pro-Pattern – switch-Expression statt Verschachtelung:
var status = (isRunning, hasError, isPending) switch
{
    (true, _, _) => "Running",
    (false, true, _) => "Error",
    (false, false, true) => "Pending",
    _ => "Unknown"
};

// ✅ Pro-Pattern – if/else statt mehrzeiligem Ternary:
string result;
if ((condition1 && condition2) || condition3)
{
    result = ComputeComplexResultA();
}
else
{
    result = ComputeComplexResultB();
}

// ✅ Pro-Pattern – Einzeilige Zuweisung ist erlaubt:
var displayName = string.IsNullOrEmpty(FullName) ? "Unbekannt" : FullName;
```

---

### 3.9 Magic Strings & Numbers Extrahierung

**Regel:** Hartcodierte Zahlen und Strings (Ausnahmen: `0`, `1`, `""`, `string.Empty`) dürfen nicht tief in den Methoden (Kategorie 8) versteckt sein. Sie **müssen** im Block 1 (Constants) als `private const` / `public const` oder in Enums extrahiert werden.

```csharp
// ❌ Anti-Pattern – Magic Strings und Numbers tief in der Methode:
public async Task DownloadAsync()
{
    if (_retryCount >= 3) { return; }                  // Magic Number
    await Task.Delay(1000);                            // Magic Number
    _toastService.Show("Download gestartet");          // Magic String
}

// ✅ Pro-Pattern – Im Konstanten-Block am Anfang der Klasse extrahiert:
private const int MaxRetryAttempts = 3;
private const int RetryDelayMilliseconds = 1000;
private const string DownloadStartedMessage = "Download gestartet";

public async Task DownloadAsync()
{
    if (_retryCount >= MaxRetryAttempts) { return; }
    await Task.Delay(RetryDelayMilliseconds);
    _toastService.Show(DownloadStartedMessage);
}
```

### 3.10 XML-Dokumentationskommentare

**Regel:** XML-Dokumentationskommentare (`/// <summary>`) sind in folgenden Fällen **Pflicht**:

| Element | Pflicht? | Begründung |
|---------|---------|------------|
| `public` / `protected` Klassen & Interfaces | ✅ Ja | Bilden die öffentliche API-Oberfläche |
| `public` / `protected` Methoden mit nicht-trivialem Verhalten | ✅ Ja | Verhalten und Parameter müssen dokumentiert sein |
| `public` Properties mit fachlicher Bedeutung | ✅ Ja | Fachlicher Kontext |
| `private` Methoden & Felder | ❌ Nein | Sprechende Benennung reicht |
| Offensichtliche Getter/Setter | ❌ Nein | Selbsterklärend |
| `[RelayCommand]`-Methoden | 🟡 Optional | Nur wenn Semantik nicht durch Name klar |

```csharp
// ✅ Pro-Pattern – Pflichtkommentar für öffentliche Klasse:
/// <summary>
/// Outbound Adapter (Taxonomie: Provider) als Factory für die Erstellung
/// von spaltenbasierten Datenimport-Providern anhand des Dateiformats.
/// </summary>
public sealed class OutboundAdapterColumnDataImporterFactoryProvider
    : IOutboundPortColumnDataImporterFactoryProvider { /* ... */ }

// ✅ Pro-Pattern – Pflichtkommentar für nicht-triviale Methode:
/// <summary>
/// Exportiert das aktuelle Download-Profil im angegebenen Format an den gewählten Speicherort.
/// </summary>
/// <param name="commandParameter">Der UI-Format-String (z. B. "Als JSON").</param>
public async Task ExportProfileAsync(object? commandParameter) { /* ... */ }

// ❌ Anti-Pattern – Überflüssiger Kommentar bei selbsterklärendem Property:
/// <summary>
/// Gets or sets the download profile name.
/// </summary>
public string DownloadProfileName { get; set; }    // Name IST selbsterklärend
```

> [!NOTE]
> Dokumentationskommentare sollen in der **Sprache des Projekts** verfasst werden (Deutsch im Projekt `Generix`). Sie beschreiben das **Was** und **Warum**, nicht das **Wie**.

---

### 3.11 var-Verwendung (Typinferenz)

**Regel:** Verwende `var`, wenn der Typ aus dem rechten Ausdruck **eindeutig ersichtlich** ist. Verwende den expliziten Typ, wenn der Typ **nicht offensichtlich** ist.

```csharp
// ✅ Pro-Pattern – var, wenn Typ offensichtlich:
var customer = new Customer();                                        // Typ: Customer (durch new)
var profileName = "Default Profile";                                  // Typ: string (durch Literal)
var transactionCount = 42;                                            // Typ: int (durch Literal)
var toastService = (IToastNotificationHandler)serviceProvider.Get();  // Typ: durch Cast
var importDialog = new ImportColumnWithPreview(importedColumnData);   // Typ: durch new
var fileExtension = Path.GetExtension(profileFilePath);               // Typ: string (bekannte API)

// ✅ Pro-Pattern – Expliziter Typ, wenn nicht offensichtlich:
DataFileFormat dataFormat = _formatMapperFactory.MapToDataFileFormat(fileExtension);  // Rückgabetyp unklar
IOutboundPortColumnDataImporterProvider importer = _columnDataImporterFactory.Create(dataFormat);

// 🟡 Grauzone – var erlaubt, wenn Methodenname den Typ impliziert:
var downloadProfile = await _downloadProfileRepository.GetByIdAsync(id);  // "Profile" im Namen
var importedColumnData = await importer.ReadColumnsAsync(filePath);       // "ColumnData" im Namen
```

> [!TIP]
> **Faustregel:** Wenn du den Typ nicht innerhalb von 2 Sekunden aus dem rechten Ausdruck ableiten kannst, schreibe ihn explizit hin.

---

### 3.12 sealed-Klassen

**Regel:** Klassen, die **nicht für Vererbung vorgesehen** sind, müssen als `sealed` markiert werden.

```csharp
// ❌ Anti-Pattern – Klasse ohne sealed, obwohl Vererbung nicht vorgesehen:
public class ViewModelXDownloadProfile : BaseViewModel { /* ... */ }
public class FrozenDownloadProfileFileFormatMapperFactory : IDownloadProfileFileFormatMapperFactory { /* ... */ }

// ✅ Pro-Pattern – sealed verhindert unbeabsichtigte Vererbung:
public sealed class ViewModelXDownloadProfile : BaseViewModel { /* ... */ }
public sealed class FrozenDownloadProfileFileFormatMapperFactory : IDownloadProfileFileFormatMapperFactory { /* ... */ }
```

**Wann `sealed`:**

| Situation | `sealed`? |
|-----------|-----------|
| ViewModels | ✅ Ja |
| Service-Implementierungen | ✅ Ja |
| Factory-Klassen | ✅ Ja |
| Adapter (Hex. Architektur) | ✅ Ja |
| Abstrakte Basisklassen | ❌ Nein (per Definition) |
| Klassen mit `virtual` Methoden | ❌ Nein |
| Klassen, die explizit für Ableitung dokumentiert sind | ❌ Nein |

> [!TIP]
> `sealed` verbessert auch die **Performance**: Der JIT-Compiler kann Methodenaufrufe auf `sealed`-Klassen devirtualisieren, was zu schnelleren Aufrufen führt.

---

### 3.13 Access Modifier (Sichtbarkeitsregeln)

**Regel:** Verwende immer den **restriktivsten** Access Modifier, der ausreicht. Sichtbarkeit ist kein Default – sie muss bewusst gewählt werden.

| Modifier | Sichtbarkeit | Wann verwenden? |
|----------|-------------|-----------------|
| `private` | Nur innerhalb der Klasse | **Standard für Felder und Hilfsmethoden** |
| `private protected` | Klasse + abgeleitete Klassen im selben Assembly | Selten, nur bei Vererbung innerhalb eines Assemblys |
| `internal` | Innerhalb des Assemblys | Für projektinterne Services, die nicht über die API exponiert werden |
| `protected` | Klasse + abgeleitete Klassen (auch assembly-übergreifend) | Bei Basisklassen, die erweitert werden sollen |
| `public` | Überall | Nur für explizit externe API-Oberflächen |

```csharp
// ❌ Anti-Pattern – Alles public:
public class DownloadService
{
    public IHttpClient _httpClient;                   // Feld public – Kapselung gebrochen
    public string FormatUrl(string path) { /* ... */ } // Hilfsmethode public ohne Grund
    public void InternalCleanup() { /* ... */ }        // Interne Logik public
}

// ✅ Pro-Pattern – Restriktivste Sichtbarkeit:
public sealed class DownloadService
{
    private readonly IHttpClient _httpClient;                     // private Feld
    private static string FormatUrl(string path) { /* ... */ }    // private Hilfsmethode
    internal void Cleanup() { /* ... */ }                          // internal, nur Assembly-weit sichtbar
    public async Task DownloadAsync(string url) { /* ... */ }     // public – echte API
}
```

> [!IMPORTANT]
> **Explizite Access Modifier Pflicht:** Schreibe den Modifier **immer** explizit hin, auch wenn `private` der Default ist. Implizite Sichtbarkeit erzwingt mentale Ableitung und ist fehleranfällig.

```csharp
// ❌ Anti-Pattern – Impliziter Modifier:
class InternalService { }            // implizit internal
string _name;                        // implizit private

// ✅ Pro-Pattern – Expliziter Modifier:
internal class InternalService { }   // explizit internal
private string _name;                // explizit private
```

---

### 3.14 Namespace- und Ordner-Kongruenz

**Regel:** Der Namespace einer Datei muss **exakt** dem physischen Ordnerpfad im Projekt entsprechen (abzüglich des Projekt-Stammverzeichnisses). Namespaces verwenden für Ordner meistens die **Pluralform** (z.B. `ViewModels`, `Services`).
Wird eine Datei verschoben, MUSS der Namespace zwingend angepasst werden.

```csharp
// ❌ Anti-Pattern – Ordnerstruktur ignoriert:
// Dateipfad: Generix.Desktop/Services/Toast/ToastService.cs
namespace Generix.Desktop; // Falsch, ignoriert Ordnerstruktur

// ✅ Pro-Pattern – Namespace spiegelt Ordnerstruktur wider:
// Dateipfad: Generix.Desktop/Services/Toast/ToastService.cs
namespace Generix.Desktop.Services.Toast;
```

---

### 3.15 this.-Verbot

**Regel:** Die Verwendung des `this.`-Qualifizierers für den Zugriff auf Klassen-Members ist **strikt verboten** (Ausnahme: Konstruktoren oder Extension Methods bei Namenskollisionen). Durch die strikte Trennung von `_camelCase` (Felder), `PascalCase` (Properties) und `camelCase` (lokale Variablen) gibt es keine Verwechslungsgefahr. `this.` bläht den Code nur visuell auf.

```csharp
// ❌ Anti-Pattern – Völlig überflüssiges this.:
this._isLoading = true;
this.DownloadProfileName = "Test";
this.LoadTransactions();

// ✅ Pro-Pattern – Clean Code ohne this.:
_isLoading = true;
DownloadProfileName = "Test";
LoadTransactions();
```

---

### 3.16 Kommentar-Hygiene (Toter Code)

**Regel:** Auskommentierter (toter) Code verschmutzt die Struktur und ist **strikt verboten**. Er muss sofort gelöscht werden. Versionskontrolle (Git) ist dafür verantwortlich, alten Code aufzubewahren, nicht die Codebase selbst. Kommentare erklären nur das **Warum**, niemals alten Code.

```csharp
// ❌ Anti-Pattern – Toter Code als Blockade:
public void Process()
{
    // var oldWay = LegacyProcess();
    // if (oldWay.IsSuccess) return;
    
    var newWay = ModernProcess();
}

// ✅ Pro-Pattern – Historischer Müll entfernt:
public void Process()
{
    var newWay = ModernProcess();
}
```


---

### 3.17 Modifier-Reihenfolge

**Regel:** C# erlaubt verschiedene Reihenfolgen für Modifikatoren, aber die Enterprise-Standard-Reihenfolge ist strikt vorgegeben. Die Schlüsselwörter müssen in exakt dieser Reihenfolge stehen:
`Access Modifier` ➔ `static` ➔ `Sonstige (virtual, abstract, override, sealed)` ➔ `readonly` ➔ `async`

```csharp
// ❌ Anti-Pattern – Chaotische Modifier-Reihenfolge:
static public readonly string Name1 = "A";
readonly private static int Count = 5;
async public virtual Task Process() { }

// ✅ Pro-Pattern – Strikte, einheitliche Reihenfolge:
public static readonly string Name1 = "A";
private static readonly int Count = 5;
public virtual async Task Process() { }
```

---

### 3.18 Vertikaler Parameter-Umbruch (Line Wrapping)

**Regel:** Sobald die Parameter-Liste einer Methode (bei der Definition oder beim Aufruf) zu lang für eine Zeile wird (Richtwert: ~100–120 Zeichen oder mehr als 3 Parameter mit langen Namen), muss **jeder Parameter auf eine eigene, neue Zeile** gesetzt werden. Das wilde Mischen von Parametern in einer Zeile ist verboten.

```csharp
// ❌ Anti-Pattern – Unleserlich durch chaotischen Umbruch:
public async Task DownloadFileAsync(string sourceUrl, string destinationPath, 
    int retryCount, bool overwriteExisting, CancellationToken token) { /* ... */ }

// ✅ Pro-Pattern – Jeder Parameter bekommt seine eigene Zeile (vertikale Ausrichtung):
public async Task DownloadFileAsync(
    string sourceUrl,
    string destinationPath,
    int retryCount,
    bool overwriteExisting,
    CancellationToken cancellationToken) 
{ 
    /* ... */ 
}
```

---

### 3.19 Allman-Klammer-Stil (Brace Placement)

**Regel:** In C# steht die öffnende geschweifte Klammer `{` immer auf einer **neuen, eigenen Zeile** (Allman Style). Die Platzierung am Ende der vorherigen Zeile (K&R / Java-Stil) ist verboten. Die einzige Ausnahme sind einzeilige Properties.

```csharp
// ❌ Anti-Pattern – Java/JavaScript-Stil:
public void ProcessData() {
    if (isActive) {
        Execute();
    } else {
        Abort();
    }
}

// ✅ Pro-Pattern – C# Allman-Stil:
public void ProcessData()
{
    if (isActive)
    {
        Execute();
    }
    else
    {
        Abort();
    }
}
```

---

### 3.20 Methoden-Überladungen (Overloads) zusammenhalten

**Regel:** Wenn eine Methode überladen wird (gleicher Name, andere Parameter), **müssen** alle Überladungen direkt untereinander in der Methoden-Kategorie stehen. Sie dürfen nicht durch andere Methoden getrennt werden. Sie sollten nach der Anzahl der Parameter sortiert sein.

```csharp
// ❌ Anti-Pattern – Überladungen sind verstreut:
public void Initialize() { /* ... */ }
public void Dispose() { /* ... */ }
public void Initialize(string config) { /* ... */ }

// ✅ Pro-Pattern – Überladungen bilden einen visuellen Block:
public void Initialize() { /* ... */ }
public void Initialize(string config) { /* ... */ }

public void Dispose() { /* ... */ }
```

---

### 3.21 Arrow-Anti-Pattern (Nesting-Tiefe)

**Regel:** Strukturell ist eine maximale Einrücktiefe von **3 Ebenen** innerhalb einer Methode erlaubt (z.B. Methode ➔ `foreach` ➔ `if`). Alles darüber hinaus muss zwingend durch **Early Returns (Bouncer Pattern)** abgeflacht oder in eine private Hilfsmethode extrahiert werden, damit der Code linksbündig bleibt.

```csharp
// ❌ Anti-Pattern – Der Code wandert wie ein Pfeil nach rechts:
public void ProcessUsers(List<User> users)
{
    if (users != null)
    {
        foreach (var user in users)
        {
            if (user.IsActive)
            {
                if (user.HasSubscription)
                {
                    // Code ...
                }
            }
        }
    }
}

// ✅ Pro-Pattern – Flach durch Early Returns (Bouncer Pattern):
public void ProcessUsers(List<User>? users)
{
    if (users is null) return;

    foreach (var user in users)
    {
        if (!user.IsActive) continue;
        if (!user.HasSubscription) continue;

        // Code ...
    }
}
```

---

### 3.22 Pattern Matching: `is null` / `is not null` statt `==`/`!=`

**Regel:** Für Null-Vergleiche wird immer `is null` bzw. `is not null` verwendet, niemals `== null` oder `!= null`. `is null` kann nicht durch einen überladenen `==`-Operator verfälscht werden und ist die vom .NET-Team empfohlene Konvention.

```csharp
// ❌ Anti-Pattern – Operator-basierter Null-Vergleich:
if (customer == null) { return; }
if (order != null) { Process(order); }
while (currentNode != null) { currentNode = currentNode.Next; }

// ✅ Pro-Pattern – Pattern-Matching-basierter Null-Vergleich:
if (customer is null) { return; }
if (order is not null) { Process(order); }
while (currentNode is not null) { currentNode = currentNode.Next; }
```

> [!NOTE]
> **Ausnahme:** Bei `record`-Typen mit überladenem `==`-Operator, der bewusst Wert-Semantik prüft (nicht Referenz-Null-Check), ist `== null` weiterhin zulässig, sofern das Verhalten explizit gewollt ist. Im Zweifel: `is null` verwenden – es ist niemals falsch.

---

### 3.23 Collection-Expressions (`[]`) statt `new List<T>()` / `new T[]`

**Regel:** Für die Initialisierung von Collections und Arrays wird ab C# 12 die **Collection-Expression-Syntax** (`[]`) bevorzugt, sofern der Zieltyp aus dem Kontext eindeutig ableitbar ist. Sie ist kürzer, einheitlich über alle Collection-Typen hinweg und vermeidet redundante Typangaben.

```csharp
// ❌ Anti-Pattern – Klassische Konstruktor-Syntax:
private readonly List<string> _supportedExtensions = new List<string>();
private readonly ObservableCollection<DownloadTransaction> _transactions = new ObservableCollection<DownloadTransaction>();
public string[] GetDefaultHeaders() => new string[] { "Id", "Name", "CreatedAt" };
public List<int> GetEmptyList() => new List<int>();

// ✅ Pro-Pattern – Collection-Expressions:
private readonly List<string> _supportedExtensions = [];
private readonly ObservableCollection<DownloadTransaction> _transactions = [];
public string[] GetDefaultHeaders() => ["Id", "Name", "CreatedAt"];
public List<int> GetEmptyList() => [];
```

> [!TIP]
> **Faustregel:** Wenn der Zieltyp links vom `=` oder durch den Rückgabetyp eindeutig feststeht, verwende `[]`. Bleibt bei `var` unklar, welcher konkrete Collection-Typ gemeint ist (z. B. `List<T>` vs. `T[]`), bleibt der explizite `new`-Aufruf oder ein expliziter linksseitiger Typ vorzuziehen.

---

### 3.24 Records: `record` vs. `record struct` vs. `class`

**Regel:** Für unveränderliche Datenträger mit **Wert-Gleichheit** (zwei Instanzen mit denselben Werten gelten als gleich) wird `record` bzw. `record struct` verwendet, nicht `class`. Die Wahl zwischen `record` (Referenztyp) und `record struct` (Werttyp) folgt denselben Kriterien wie `class` vs. `struct` (siehe Performance-Leitfaden, Kapitel 13).

| Typ | Semantik | Ideal für |
|-----|----------|-----------|
| `class` | Referenz, Identität, veränderlich | Entities mit Lebenszyklus, DI-Services |
| `record` (= `record class`) | Referenz, Wert-Gleichheit, `with`-Expressions | DTOs, Events, unveränderliche Snapshots |
| `record struct` | Wert, Wert-Gleichheit, Stack-Allokation | Kleine, häufig kopierte, unveränderliche Datenpakete (≤ 16 Bytes, siehe Performance-Leitfaden 13.2) |

```csharp
// ❌ Anti-Pattern – class für einen reinen, unveränderlichen Datenträger:
public sealed class DownloadProgressSnapshot
{
    public int Completed { get; init; }
    public int Total { get; init; }

    public override bool Equals(object? obj) => /* manuelle Equals-Implementierung */ false;
    public override int GetHashCode() => /* manuelle Hash-Implementierung */ 0;
}

// ✅ Pro-Pattern – record für Wert-Gleichheit ohne Boilerplate:
private sealed record DownloadProgressSnapshot(int Completed, int Total);

// ✅ Pro-Pattern – record struct für kleine, häufig kopierte Werte:
public readonly record struct Point2D(double X, double Y);
```

> [!WARNING]
> **`record class` ist bereits implizit `sealed`, wenn es nicht explizit für Vererbung geöffnet wird** – ein zusätzliches `sealed`-Keyword vor `record` ist erlaubt, aber redundant, sofern kein `record` davon erbt. Achte bei positional records mit vielen Parametern (> 4) trotzdem auf die Parameter-Umbruch-Regel (3.18).

---

### 3.25 Nullable Reference Types, `required` & `init`-only Properties

**Regel:** Das Projekt kompiliert mit `<Nullable>enable</Nullable>`. Jede Referenztyp-Property/-Parameter ohne `?` gilt als **verbindlich nicht-null** und muss entweder im Konstruktor gesetzt, mit `required` markiert oder mit einem Default-Wert versehen werden. `init` wird für Properties verwendet, die nach der Objekterstellung **nie mehr verändert** werden dürfen.

```csharp
// ❌ Anti-Pattern – Nicht-nullable Property ohne Zusicherung, dass sie gesetzt wird:
public sealed class CreateProfileRequest
{
    public string ProfileName { get; set; }        // ⚠️ CS8618-Warnung: evtl. null bei Erstellung
    public string SourceUrl { get; set; }           // ⚠️ Kein Schutz vor nachträglicher Änderung
}

// ✅ Pro-Pattern – required erzwingt Objektinitialisierer, init verhindert nachträgliches Ändern:
public sealed class CreateProfileRequest
{
    public required string ProfileName { get; init; }
    public required string SourceUrl { get; init; }
    public string? Description { get; init; }        // Explizit optional -> nullable
}

// Verwendung: Compiler erzwingt das Setzen aller "required"-Properties
var request = new CreateProfileRequest
{
    ProfileName = "Q1-Report",
    SourceUrl = "https://example.com/data.csv",
};
```

| Modifier-Kombination | Bedeutung | Wann verwenden |
|-----------------------|-----------|-----------------|
| `{ get; set; }` | Jederzeit änderbar | Veränderlicher Zustand (ViewModels, Entities) |
| `{ get; init; }` | Nur bei Objekterstellung setzbar | Unveränderliche DTOs, Value Objects |
| `required` + `{ get; init; }` | Muss bei Objekterstellung gesetzt werden, danach unveränderlich | Pflichtfelder in DTOs/Requests ohne Konstruktor-Zwang |
| `= defaultValue;` ohne `required` | Optional, hat sinnvollen Default | Optionale Konfigurationswerte |

> [!IMPORTANT]
> **`required` ersetzt keinen Konstruktor-Zwang bei Domain-Entities.** Für Domain-Objekte mit Invarianten (siehe DDD-Prinzipien) bleibt ein Konstruktor mit Validierung die richtige Wahl. `required`/`init` ist primär für **DTOs, Requests und Value Objects** gedacht, die keine Geschäftslogik kapseln.

---

## 4. Schritt 2: Refactoring-Empfehlungen & Zusammenfassung

### Priorisierungsmatrix

| Priorität | Kategorie | Wann? |
|-----------|-----------|-------|
| 🔴 **Kritisch** | Laufzeit-Exceptions, Datenverlust, Sicherheitsprobleme | Sofort beheben |
| 🟠 **Hoch** | SRP-Verletzungen, fehlende Null-Checks, verschluckte Exceptions | Im selben Sprint |
| 🟡 **Mittel** | Namensgebungs-Verstöße, Sortier-Reihenfolge, fehlende Konstanten | Bei nächster Berührung |
| 🟢 **Niedrig** | Kosmetische Verbesserungen, optionale Vereinfachungen | Nice-to-have |

### Empfehlungsformat

```
### [Priorität] Kurzbeschreibung

**Datei:** `DateiName.cs` (Zeile X–Y)
**Problem:** Was ist das konkrete Problem?
**Begründung:** Welches Prinzip wird verletzt? (z. B. SRP, Regel 2.5)
**Lösung:** Was genau soll geändert werden?
```

**Beispiel:**

```
### 🟡 Boolean ohne Zustandspräfix in Zeile 82

**Datei:** `ViewModelXDownloadProfile.cs` (Zeile 82)
**Problem:** Das Feld `_loading` verwendet keinen Zustandspräfix.
**Begründung:** Verstößt gegen Namensregel 2.5 (Boolean-Naming).
**Lösung:** Umbenennen in `_isLoading`.
```

---

## 5. ✅ Klassen-Review Checkliste

### Schritt 1 – Struktur & Namensgebung

- [ ] **Generische Namen (2.1):** Keine `data`, `item`, `obj`, `temp`, `result`, `value`.
- [ ] **Ein-Buchstaben-Variablen (2.2):** Nur `i`/`j`/`k` in `for`-Schleifen und triviale Lambdas.
- [ ] **Technische Namen (2.3):** Kein Typname im Bezeichner (`userList` → `users`).
- [ ] **Abkürzungen (2.4):** Vollständig ausgeschrieben (`usr` → `user`, `cfg` → `configuration`).
- [ ] **Boolean-Naming (2.5):** Alle Booleans mit `Is`, `Has`, `Can`, `Should`, `Was`, `Are`.
- [ ] **Async-Suffix (2.6):** Alle async-Methoden enden mit `Async`.
- [ ] **Verben & Nomen (2.7):** Methoden = Verben, Properties = Nomen/Adjektive.
- [ ] **Keine Negationen (2.8):** Keine doppelten Negationen, positive Boolean-Namen.
- [ ] **Collection-Naming (2.9):** Pluralform ohne technischen Suffix.
- [ ] **Command-Naming (2.10):** Kein redundantes „Command" in `[RelayCommand]`-Methoden.
- [ ] **Event-Naming (2.11):** Partizip-Formen, kein `On`-Präfix im Event-Namen.
- [ ] **Deconstruction (2.12):** Benannte Tupel und Dictionary Deconstruction statt `Item1` / `Key`.
- [ ] **Generische Parameter (2.13):** Fachliche `T...` Namen bei mehreren Parametern.
- [ ] **Architektur-Suffixe (2.14):** Saubere Trennung durch `Dto`, `Request`, `Response`, `ViewModel`.
- [ ] **Exception/Attribute (2.15):** Zwingendes Suffix `...Exception` und `...Attribute`.
- [ ] **nameof-Zwang (2.16):** Typsichere `nameof`-Verwendung statt Magic Strings.
- [ ] **Verbot von And/Or (2.17):** Keine Klassennamen mit `And` / `Or` (SRP-Verletzung).
- [ ] **Casing (3.1):** PascalCase/\_camelCase/camelCase korrekt angewandt.
- [ ] **Using-Sortierung (3.2):** System/Drittanbieter → Projekt-Namespaces, alphabetisch.
- [ ] **Kategorien-Reihenfolge (3.3.2):** Constants → Fields → Observable Properties → Properties → Events → Constructors → Commands → Methods → Nested Types.
- [ ] **4-Block-Sortierung (3.3.1):** Dependencies → Primitive → Enums → Komplexe Typen.
- [ ] **Partial Methods (3.3.3):** Direkt unter zugehöriger `[ObservableProperty]`.
- [ ] **Nested Types (3.3.4):** Innere Typen stehen am Ende der Klasse (Kategorie 9).
- [ ] **Static Members (3.3.5):** Vor Instanz-Members in ihrer Kategorie.
- [ ] **Extension Methods (3.3.6):** `static` Klasse, `...Extensions` Suffix, `this` Parameter.
- [ ] **Keine `#region`-Blöcke (3.3.7):** Regions im gesamten Code verboten.
- [ ] **Eine Klasse pro Datei (3.3.8):** Dateiname = Klassenname. Keine Sammeldateien.
- [ ] **Partial Classes (3.3.9):** 9-Kategorien-Sortierung gilt pro Datei, nicht klassenübergreifend.
- [ ] **Parameter-Reihenfolge (3.4):** Pflicht → Optional → Callbacks → CancellationToken.
- [ ] **Primary Constructors (3.5):** Parameter in `_camelCase`-Felder zugewiesen.
- [ ] **Leerzeilen (3.6):** Konsistent zwischen Kategorien und innerhalb Methoden.
- [ ] **Expression-Bodied (3.7):** `=>` für einzeilig, `{ }` für mehrzeilig.
- [ ] **Ternary Operator (3.8):** Nur einzeilig, keine Verschachtelungen.
- [ ] **Magic Strings/Numbers (3.9):** Als Konstanten in Kategorie 1 extrahiert.
- [ ] **XML-Docs (3.10):** Pflicht für öffentliche Klassen und nicht-triviale Methoden.
- [ ] **var-Verwendung (3.11):** `var` nur bei offensichtlichem Typ.
- [ ] **sealed (3.12):** Klassen ohne Vererbungsabsicht als `sealed` markiert.
- [ ] **Access Modifier (3.13):** Restriktivster Modifier, immer explizit.
- [ ] **Namespace-Kongruenz (3.14):** Namespace entspricht exakt dem Dateipfad.
- [ ] **this.-Verbot (3.15):** Kein überflüssiger `this.`-Qualifizierer.
- [ ] **Kommentar-Hygiene (3.16):** Auskommentierter toter Code ist gelöscht.
- [ ] **Modifier-Reihenfolge (3.17):** `Access` → `static` → `virtual/override` → `readonly` → `async`.
- [ ] **Parameter-Umbruch (3.18):** Lange Parameterlisten vertikal umgebrochen (1 pro Zeile).
- [ ] **Allman-Klammer (3.19):** Öffnende `{` immer auf neuer Zeile (kein Java-Stil).
- [ ] **Überladungen (3.20):** Overloads von Methoden stehen direkt untereinander.
- [ ] **Nesting-Tiefe (3.21):** Max 3 Ebenen, Nutzung von Early Returns (Bouncer Pattern).
- [ ] **Pattern Matching (3.22):** `is null` / `is not null` statt `==`/`!= null`.
- [ ] **Collection-Expressions (3.23):** `[]` statt `new List<T>()` / `new T[]` wo eindeutig.
- [ ] **Records (3.24):** `record`/`record struct` für Wert-Gleichheit statt `class` mit manuellem Equals.
- [ ] **Nullable/required/init (3.25):** Pflichtfelder mit `required`, unveränderliche Properties mit `init`.
### Schritt 2 – Refactoring

- [ ] **Priorisierte Empfehlungen:** Alle Findings mit 🔴🟠🟡🟢 dokumentiert.
- [ ] **Umsetzbare Lösungen:** Konkrete Lösung, nicht nur Problembeschreibung.
