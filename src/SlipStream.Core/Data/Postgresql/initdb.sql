﻿-- 注意，此文件中，尽管“GO”语句不是 PostgreSQL 支持的 SQL 语言的一部分，
-- 但是每个 SQL 语句之后仍然需要一个 GO 命令将其与其它语句隔开

CREATE TABLE core_meta_entity (
    _id BIGSERIAL NOT NULL,
    "name" VARCHAR NOT NULL UNIQUE,
    label VARCHAR,
    info TEXT,
    module VARCHAR NOT NULL,
    PRIMARY KEY(_id)
);
GO

CREATE INDEX index_core_meta_entity_name ON core_meta_entity ("name");
GO

CREATE TABLE core_meta_field (
    _id BIGSERIAL NOT NULL,
    "module" VARCHAR NOT NULL,
    meta_entity BIGINT NOT NULL,
    "name" VARCHAR NOT NULL,
    "required" BOOLEAN NOT NULL,
    "readonly" BOOLEAN NOT NULL,
    relation VARCHAR,
    label VARCHAR,
    "type" VARCHAR NOT NULL,
    help TEXT,
    PRIMARY KEY(_id),
    FOREIGN KEY(meta_entity) REFERENCES core_meta_entity(_id) ON DELETE CASCADE
);
GO

CREATE INDEX index_core_meta_field_name ON core_meta_field ("name");
GO

CREATE SEQUENCE "core_module__id_seq";
GO

CREATE TABLE core_module (
    _id BIGINT DEFAULT NEXTVAL('core_module__id_seq') NOT NULL,
    "name" VARCHAR(128) NOT NULL UNIQUE,
    label VARCHAR(256),
    "state" VARCHAR(16) NOT NULL,
    demo BOOLEAN NOT NULL DEFAULT TRUE,
    author VARCHAR(128),
    url VARCHAR(128),
    version VARCHAR(64),
    info TEXT,
    license VARCHAR(32),
    PRIMARY KEY(_id)
);
GO

CREATE UNIQUE INDEX index_core_module_name ON core_module ("name");
GO

CREATE TABLE core_module_dependency (
    _id BIGSERIAL NOT NULL,
    name VARCHAR(128) NOT NULL,
    module BIGINT NOT NULL,
    PRIMARY KEY(_id),
    FOREIGN KEY (module) REFERENCES core_module ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX index_core_module_dependency_name ON core_module_dependency ("name");
GO

CREATE TABLE core_user (
    _id BIGSERIAL NOT NULL,
    PRIMARY KEY(_id)
);
GO

CREATE TABLE core_organization (
    _id BIGSERIAL NOT NULL,
    PRIMARY KEY(_id)
);
GO


CREATE TABLE core_entity_data (
	_id BIGSERIAL NOT NULL,
	name VARCHAR(128) NOT NULL,
	module VARCHAR(64) NOT NULL,
	entity VARCHAR(64) NOT NULL,
	ref_id BIGINT NOT NULL,
	value TEXT,
	PRIMARY KEY(_id)
);
GO

CREATE UNIQUE INDEX index_core_entity_data_name ON core_entity_data ("name");
GO

CREATE TABLE core_session (
	_id BIGSERIAL NOT NULL,
	"token" VARCHAR(128) NOT NULL,
	start_time TIMESTAMP NOT NULL,
	last_activity_time TIMESTAMP NOT NULL,
	userid BIGINT NOT NULL,
	"login" VARCHAR(128) NOT NULL,
	PRIMARY KEY(_id)
);
GO

CREATE UNIQUE INDEX index_core_sessionid ON core_session("token");
GO

-- 下面全部是存储过程/函数

-- 一个小技巧，如果没有安装 pl/sql 就安装，否则忽略

CREATE OR REPLACE FUNCTION make_plpgsql()
RETURNS VOID
LANGUAGE SQL
AS $$
CREATE LANGUAGE plpgsql;
$$;
GO
 
SELECT
    CASE
    WHEN EXISTS(
        SELECT 1
        FROM pg_catalog.pg_language
        WHERE lanname='plpgsql'
    )
    THEN NULL
    ELSE make_plpgsql() END;
GO
 
DROP FUNCTION make_plpgsql();
GO

-- 处理树的函数

CREATE OR REPLACE FUNCTION tree_update_for_creation(
	table_name VARCHAR, self_id BIGINT, parent_id BIGINT DEFAULT NULL) 
RETURNS VOID AS $$
DECLARE
    _left BIGINT;
    _right BIGINT;
    rhs_value BIGINT;
    cmd VARCHAR;
BEGIN

    EXECUTE 'LOCK TABLE ' || table_name::regclass;

    IF parent_id IS NOT NULL THEN
        EXECUTE 'SELECT _left, _right FROM ' || table_name::regclass || ' WHERE _id = ' || parent_id
            INTO _left, _right;
        IF _right - _left = 1 THEN
            rhs_value := _left;
        ELSE
            rhs_value := _right - 1; --添加到集合的末尾
        END IF;
    ELSE --没有就作为最后一个根节点
        EXECUTE 'SELECT COALESCE(MAX(_right), 0) FROM ' || table_name::regclass || ' WHERE _left >= 0' 
            INTO rhs_value;    
    END IF;    
    
    EXECUTE 'UPDATE ' 
        || table_name::regclass
        || ' SET _right = _right + 2 WHERE _right > ' || rhs_value;
        
    EXECUTE 'UPDATE ' || table_name::regclass
        || ' SET _left = _left + 2 WHERE _left > ' || rhs_value;

    EXECUTE 'UPDATE ' || table_name::regclass || ' SET _left=' || rhs_value + 1
        || ', _right=' || rhs_value + 2 || ' WHERE _id=' || self_id;
        
END;
$$ LANGUAGE plpgsql;
GO

-- 删除树形表的节点并更新表
CREATE OR REPLACE FUNCTION tree_update_for_deletion(table_name VARCHAR, ids BIGINT ARRAY)
RETURNS VOID AS $$
DECLARE
    width BIGINT NOT NULL := 0;
    node RECORD;
BEGIN    
    EXECUTE 'LOCK TABLE ' || table_name::regclass;

    FOR node IN EXECUTE 'SELECT _id, _left, _right FROM ' || table_name::regclass || 
        ' WHERE _id = ANY($1) ORDER BY _right - _left ASC, _right DESC' USING ids LOOP      
        width := node._right - node._left;
        EXECUTE 'DELETE FROM ' || table_name::regclass || 
            ' WHERE _left BETWEEN ' || node._left || ' AND ' || node._right; 

        EXECUTE 'UPDATE ' || table_name::regclass || ' SET _right = _right - ' || width + 1 ||
            ' WHERE _right > ' || node._right;

        EXECUTE 'UPDATE ' || table_name::regclass || ' SET _left = _left - ' || width + 1 ||
            ' WHERE _left > ' || node._left;

    END LOOP;
END;
$$ LANGUAGE plpgsql;
GO

