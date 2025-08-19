param(
    [Parameter(Mandatory=$true)]
    [string]$ConnectionString,
    
    [Parameter(Mandatory=$true)]
    [string]$Collection,
    
    [Parameter(Mandatory=$false)]
    [string]$FilePath = ".\.dump\posttooluse-raw-dump.txt"
)

function Import-ToMongoDB {
    param(
        [string]$ConnStr,
        [string]$Coll,
        [string]$File
    )
    
    # Verifica che il file esista
    if (-not (Test-Path $File)) {
        Write-Error "File $File non trovato!"
        return
    }
    
    # Verifica che mongosh sia disponibile
    try {
        $null = Get-Command mongosh -ErrorAction Stop
    }
    catch {
        Write-Error "mongosh non trovato! Assicurati che MongoDB Shell sia installato e nel PATH."
        return
    }
    
    Write-Host "Iniziando l'importazione da $File verso collection $Coll..." -ForegroundColor Green
    
    # Crea un file temporaneo per il contenuto rimanente
    $tempFile = [System.IO.Path]::GetTempFileName()
    
    try {
        $lineNumber = 1
        $processedLines = 0
        $errors = 0
        
        # Leggi il file riga per riga
        $lines = Get-Content $File
        $remainingLines = @()
        
        foreach ($line in $lines) {
            if ($line.Trim() -eq "") {
                $remainingLines += $line
                $lineNumber++
                continue
            }
            
            try {
                # Valida che la riga sia un JSON valido
                $jsonObject = $line | ConvertFrom-Json
                
                # Crea un comando MongoDB per inserire il documento
                $mongoCmd = @"
db.getCollection('$Coll').insertOne($line);
"@
                
                # Esegui il comando MongoDB
                $result = $mongoCmd | mongosh $ConnStr --quiet
                
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "✓ Riga $lineNumber inserita con successo" -ForegroundColor DarkGreen
                    $processedLines++
                    # Non aggiungere questa riga al file temporaneo (sarà eliminata)
                }
                else {
                    Write-Warning "Errore durante l'inserimento della riga $lineNumber`: $result"
                    $remainingLines += $line
                    $errors++
                }
            }
            catch {
                Write-Warning "Errore nel parsing JSON della riga $lineNumber`: $($_.Exception.Message)"
                $remainingLines += $line
                $errors++
            }
            
            $lineNumber++
        }
        
        # Aggiorna il file originale con le righe rimanenti (rimuove quelle processate)
        $remainingLines | Set-Content $File -Encoding UTF8
        
        Write-Host "`nRiepilogo:" -ForegroundColor Yellow
        Write-Host "- Righe processate con successo: $processedLines" -ForegroundColor Green
        Write-Host "- Errori: $errors" -ForegroundColor Red
        Write-Host "- Righe rimanenti nel file: $($remainingLines.Count)" -ForegroundColor Cyan
        
    }
    finally {
        # Pulisci il file temporaneo se esiste
        if (Test-Path $tempFile) {
            Remove-Item $tempFile -Force
        }
    }
}

# Esegui l'importazione
Import-ToMongoDB -ConnStr $ConnectionString -Coll $Collection -File $FilePath