# Katalog Części

Nowoczesny system webowy do zarządzania katalogiem części samochodowych oraz przygotowywania ogłoszeń sprzedażowych.

<img width="1911" height="1037" alt="CarParts-4" src="https://github.com/user-attachments/assets/a077ac1d-2713-4a93-8cd0-4c8f7ffcdb03" />


Aplikacja została zaprojektowana z myślą o codziennej pracy warsztatu i magazynu części — szybkie wyszukiwanie, zdjęcia, statusy sprzedaży, backup danych oraz podstawowe statystyki w jednym miejscu.

## ✨ Features

* 📦 katalog części samochodowych
* 📸 obsługa wielu zdjęć dla każdej części
* 🔎 szybkie wyszukiwanie części, marek i modeli
* ↕️ sortowanie według ceny i daty
* 🔴 oznaczanie części jako **SPRZEDANE**
* 📝 przygotowywanie lokalnych ogłoszeń
* 💾 wykonywanie backupów bazy danych
* 📊 statystyki katalogu i ogłoszeń
* 🔐 logowanie administratora
* 🔑 bezpieczne przechowywanie haseł z wykorzystaniem BCrypt
* 🛡️ autoryzacja dostępu do chronionych funkcji
* ⏱️ cooldown dla backupów — ograniczenie zbyt częstego wykonywania kopii
* ⏱️ cooldown dla tworzenia ogłoszeń — zabezpieczenie przed przypadkowym lub masowym dodawaniem
* 🐳 uruchamianie aplikacji w Dockerze


## 🛡️ Ochrona aplikacji

System posiada mechanizmy ograniczające wykonywanie operacji w zbyt krótkich odstępach czasu.

Dotyczy to między innymi:

**Backup**

Po wykonaniu backupu uruchamiany jest czasowy cooldown, który uniemożliwia natychmiastowe wykonywanie kolejnych kopii.

**Tworzenie ogłoszeń**

Dodawanie kolejnych ogłoszeń jest również ograniczone czasowo. Mechanizm działa po stronie serwera, dzięki czemu ograniczenie nie jest wyłącznie elementem interfejsu użytkownika.

Dzięki temu aplikacja jest chroniona nie tylko przed przypadkowymi kliknięciami, ale również przed prostym omijaniem ograniczeń po stronie frontendu.

<img width="1350" height="444" alt="CarParts-5" src="https://github.com/user-attachments/assets/bee5abf0-e48a-4c14-bfbd-6903d7b81497" />


## 🔐 Administracja

Dostęp do funkcji administracyjnych zabezpieczony jest przez uwierzytelnianie oparte na cookie oraz role użytkowników.

Hasła administratorów nie są przechowywane w postaci jawnej. System wykorzystuje **BCrypt** do przechowywania i weryfikacji hashy haseł.

## 🐳 Uruchomienie

Wymagania:

* .NET 8
* Docker

### Uruchomienie lokalne

```bash
dotnet run
```

### Docker

Zbudowanie obrazu:

```bash
docker build -t parts-catalog .
```

Uruchomienie kontenera:

```bash
docker run -d \
  --name parts-catalog \
  -p 7088:8080 \
  parts-catalog
```

Aplikacja będzie dostępna pod:

```text
http://localhost:7088
```

## 🚀 Deployment

Aplikacja może zostać wdrożona na VPS z wykorzystaniem Dockera.

Typowy proces aktualizacji:

```bash
git pull
docker rm -f parts-catalog
docker build -t parts-catalog .
docker run -d --name parts-catalog -p 7088:8080 parts-catalog
```

## 🧰 Technologie

* C#
* ASP.NET Core
* Blazor
* .NET 8
* Docker
* SQLite / baza danych
* BCrypt
* HTML / CSS
* JavaScript

---

### Projekt

System został przygotowany jako dedykowane narzędzie do zarządzania katalogiem części samochodowych i obsługi procesu przygotowywania ogłoszeń sprzedażowych.





---

## Screenshots

Sprytna wyszukiwarka:

<img width="1560" height="941" alt="CarParts-2" src="https://github.com/user-attachments/assets/f3864e98-3ccf-4638-9c35-9dccc1c6fe8e" />

Edytowanie opisu:
<img width="1795" height="1034" alt="CarParts-3" src="https://github.com/user-attachments/assets/b5f8cb4c-97a2-43e9-aff3-0779913af43d" />
