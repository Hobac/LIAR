# LIAR

**LIAR (Liar Interrogates AI Responses)** is a fact-verification system that verifies factual claims in English text against Wikidata.

LIAR uses a large language model to extract claims and translate them into a restricted first-order logic representation. This representation is then compiled into SPARQL and evaluated against Wikidata. The final truth value is therefore based on structured evidence from Wikidata rather than on the judgment of the language model.

Claims are classified as `True`, `False`, or `Unknown` and the system provides the generated logical formula plus available evidence.

## Documentation

A detailed paper about LIAR is included in this repository.
This README only provides the information needed to set up and run the system. 

## Requirements

- .NET 9
- An OpenAI or DeepSeek API key
- Internet access

## Setup

### 1. Configure the LLM

Copy:
```text
Backend/LLM/api_key_example.txt
```

to:
```text
Backend/LLM/api_key.txt
```

Then configure the provider, model, and API key as descriped in the example file.
Use a model available to on your account. Do not commit or share `api_key.txt`.

### 2. Build the Backend

Compile the backend in release mode:

```powershell
dotnet build .\Backend\LIAR_backend.csproj -c Release
```

Allways rebuild the project after changing the API configuration.

## Running LIAR

Start the backend with:

```powershell
Set-Location .\Backend\bin\Release\net9.0
.\LIAR_backend.exe
```

A successful start prints:

```text
Server listening on http://localhost:8080/
```

The frontend is located in:

```text
Frontend/
```

Open `index.html` to launch the frontend. 
It automatically connects to the running backend. 
A green status indicator in the top-right corner shows that the connection 
to the backend was established successfully.

## Testing LIAR

A file containing natural-language claims for testing is provided in the repository.