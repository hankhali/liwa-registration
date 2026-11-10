-- Creates the application role and database on a local PostgreSQL.
-- Run once as the postgres superuser:
--   psql -h localhost -U postgres -f scripts/create-db.sql
-- Safe to re-run: existing role/database are left in place (password is reset).
--
-- CREATEDB lets the integration tests create a uniquely named throwaway
-- database per run and drop it afterwards. Remove it on a production server:
--   ALTER ROLE liwa NOCREATEDB;

SELECT 'CREATE ROLE liwa LOGIN CREATEDB'
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'liwa')
\gexec

ALTER ROLE liwa WITH LOGIN CREATEDB PASSWORD 'liwa';

SELECT 'CREATE DATABASE liwa OWNER liwa ENCODING ''UTF8'' TEMPLATE template0'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'liwa')
\gexec

GRANT ALL PRIVILEGES ON DATABASE liwa TO liwa;
