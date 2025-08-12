#!/bin/bash

CERT_DIR="/Users/dakotabrown/Desktop/CodingProjects/GameServer/GameServer"
PFX_FILE="$CERT_DIR/server.pfx"
CRT_FILE="$CERT_DIR/server.crt"
KEY_FILE="$CERT_DIR/server.key"

if [ ! -f "$PFX_FILE" ]; then
    echo "Certificate files not found. Generating new certificates..."

    # Generate server.crt and server.key
    openssl req -x509 -newkey rsa:2048 -keyout "$KEY_FILE" -out "$CRT_FILE" -days 365 -nodes -subj "/C=US/ST=California/L=San Francisco/O=MyCompany/OU=MyOrg/CN=localhost"

    # Generate server.pfx
    openssl pkcs12 -export -out "$PFX_FILE" -inkey "$KEY_FILE" -in "$CRT_FILE" -passout pass:

    echo "Certificates generated successfully."
else
    echo "Certificate files already exist. Skipping generation."
fi
