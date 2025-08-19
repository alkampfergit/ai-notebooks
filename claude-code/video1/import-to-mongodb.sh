#!/bin/bash

# Script per importare dati JSON da un file verso MongoDB
# Usage: ./import-to-mongodb.sh <connection_string> <collection> [file_path]

set -euo pipefail  # Exit on error, undefined var, pipe fail

# Funzioni di utilità per i colori
red() { echo -e "\033[31m$*\033[0m"; }
green() { echo -e "\033[32m$*\033[0m"; }
yellow() { echo -e "\033[33m$*\033[0m"; }
cyan() { echo -e "\033[36m$*\033[0m"; }

# Verifica parametri
if [ "$#" -lt 2 ]; then
    red "Errore: Parametri insufficienti!"
    echo "Usage: $0 <connection_string> <collection> [file_path]"
    echo "  connection_string: Stringa di connessione MongoDB"
    echo "  collection:        Nome della collection MongoDB"
    echo "  file_path:         Percorso del file (default: ./.dump/posttooluse-raw-dump.txt)"
    exit 1
fi

CONNECTION_STRING="$1"
COLLECTION="$2"
FILE_PATH="${3:-./.dump/posttooluse-raw-dump.txt}"

# Verifica che il file esista
if [ ! -f "$FILE_PATH" ]; then
    red "Errore: File $FILE_PATH non trovato!"
    exit 1
fi

# Verifica che mongosh sia disponibile
if ! command -v mongosh &> /dev/null; then
    red "Errore: mongosh non trovato! Assicurati che MongoDB Shell sia installato e nel PATH."
    exit 1
fi

green "Iniziando l'importazione da $FILE_PATH verso collection $COLLECTION..."

# Crea file temporaneo per le righe rimanenti
temp_file=$(mktemp)
trap "rm -f $temp_file" EXIT

line_number=1
processed_lines=0
errors=0

# Leggi il file riga per riga
while IFS= read -r line || [ -n "$line" ]; do
    # Salta righe vuote
    if [ -z "$(echo "$line" | tr -d '[:space:]')" ]; then
        echo "$line" >> "$temp_file"
        ((line_number++))
        continue
    fi
    
    # Valida che la riga sia un JSON valido usando jq
    if echo "$line" | jq . >/dev/null 2>&1; then
        # Crea un comando MongoDB per inserire il documento
        mongo_cmd="db.getCollection('$COLLECTION').insertOne($line);"
        
        # Esegui il comando MongoDB
        if echo "$mongo_cmd" | mongosh "$CONNECTION_STRING" --quiet >/dev/null 2>&1; then
            green "✓ Riga $line_number inserita con successo"
            ((processed_lines++))
            # Non aggiungere questa riga al file temporaneo (sarà eliminata)
        else
            yellow "⚠ Errore durante l'inserimento della riga $line_number"
            echo "$line" >> "$temp_file"
            ((errors++))
        fi
    else
        yellow "⚠ Errore nel parsing JSON della riga $line_number"
        echo "$line" >> "$temp_file"
        ((errors++))
    fi
    
    ((line_number++))
done < "$FILE_PATH"

# Aggiorna il file originale con le righe rimanenti (rimuove quelle processate)
mv "$temp_file" "$FILE_PATH"

# Riepilogo
echo
yellow "Riepilogo:"
green "- Righe processate con successo: $processed_lines"
red "- Errori: $errors"
cyan "- Righe rimanenti nel file: $(wc -l < "$FILE_PATH")"

exit 0