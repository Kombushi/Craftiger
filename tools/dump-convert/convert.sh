#!/bin/sh
# Fetches the pinned JDBC drivers, then runs Convert.java against an HSQLDB NESQL export.
# HSQLDB is pinned to the version the exporter bundles so the on-disk format always matches.
set -eu
dir="$(cd "$(dirname "$0")" && pwd)"
lib="$dir/lib"
mkdir -p "$lib"

hsqldb="$lib/hsqldb-2.7.2.jar"
sqlite="$lib/sqlite-jdbc-3.46.0.0.jar"
slf4j="$lib/slf4j-nop-2.0.13.jar"
slf4japi="$lib/slf4j-api-2.0.13.jar"
[ -f "$hsqldb" ] || curl -fsSL -o "$hsqldb" \
    "https://repo1.maven.org/maven2/org/hsqldb/hsqldb/2.7.2/hsqldb-2.7.2.jar"
[ -f "$sqlite" ] || curl -fsSL -o "$sqlite" \
    "https://repo1.maven.org/maven2/org/xerial/sqlite-jdbc/3.46.0.0/sqlite-jdbc-3.46.0.0.jar"
[ -f "$slf4japi" ] || curl -fsSL -o "$slf4japi" \
    "https://repo1.maven.org/maven2/org/slf4j/slf4j-api/2.0.13/slf4j-api-2.0.13.jar"
[ -f "$slf4j" ] || curl -fsSL -o "$slf4j" \
    "https://repo1.maven.org/maven2/org/slf4j/slf4j-nop/2.0.13/slf4j-nop-2.0.13.jar"

exec mise x java@temurin-17 -- java -cp "$hsqldb:$sqlite:$slf4japi:$slf4j" "$dir/Convert.java" "$@"
