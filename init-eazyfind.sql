CREATE EXTENSION IF NOT EXISTS dblink;

DO
$$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_database WHERE datname = 'eazyfind'
    ) THEN
        PERFORM dblink_exec('dbname=postgres', 'CREATE DATABASE eazyfind');
    END IF;
END
$$;

DO
$$
BEGIN
    PERFORM dblink_exec(
        'dbname=eazyfind',
        'CREATE EXTENSION IF NOT EXISTS pg_trgm;'
    );
END
$$;