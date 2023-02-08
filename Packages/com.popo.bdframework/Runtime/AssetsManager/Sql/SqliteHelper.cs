﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BDFramework.Core.Tools;
using ILRuntime.CLR.Method;
using ILRuntime.CLR.TypeSystem;
using ILRuntime.CLR.Utils;
using ILRuntime.Runtime.Intepreter;
using ILRuntime.Runtime.Stack;
using SQLite;
using UnityEngine;

//这里为了方便切换Sqlite版本 将三个类放在一起
namespace BDFramework.Sql
{
    /// <summary>
    /// Sqlite 加载器
    /// </summary>
    static public class SqliteLoder
    {
        private static string testkey = "password11222!";

        /// <summary>
        /// 本地DB Path
        /// </summary>
        public readonly static string LOCAL_DB_PATH = "local.db";

        /// <summary>
        /// ServerDB Path
        /// </summary>
        public readonly static string SERVER_DB_PATH = "server.db";

        /// <summary>
        /// sql驱动对象
        /// </summary>
        static public SQLiteConnection Connection { get; set; }

        /// <summary>
        /// DB连接库
        /// </summary>
        private static Dictionary<string, SQLiteConnection> SqLiteConnectionMap =
            new Dictionary<string, SQLiteConnection>();

        /// <summary>
        /// runtime下加载，只读
        /// </summary>
        /// <param name="str"></param>
        static public void Init(AssetLoadPathType assetLoadPathType)
        {
            Connection?.Dispose();

            var path = GameConfig.GetLoadPath(assetLoadPathType);
            //用当前平台目录进行加载
            path = GetLocalDBPath(path, BApplication.RuntimePlatform);
            Connection = LoadDBReadOnly(path);
        }


        /// <summary>
        /// 加载db 只读
        /// </summary>
        static public SQLiteConnection LoadDBReadOnly(string path)
        {
            if (File.Exists(path))
            {
                BDebug.Log("DB加载路径:" + path, "red");
                SQLiteConnectionString cs =
                    new SQLiteConnectionString(path, SQLiteOpenFlags.ReadOnly, true, key: testkey);
                var con = new SQLiteConnection(cs);
                SqLiteConnectionMap[Path.GetFileNameWithoutExtension(path)] = con;
                return con;
            }
            else
            {
                Debug.LogError("DB不存在:" + path);
                return null;
            }
        }

        /// <summary>
        /// 加载db ReadWriteCreate
        /// </summary>
        static public SQLiteConnection LoadDBReadWriteCreate(string path)
        {
            BDebug.Log("DB加载路径:" + path, "red");
            SQLiteConnectionString cs = new SQLiteConnectionString(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create, true, key: testkey);
            var con = new SQLiteConnection(cs);
            SqLiteConnectionMap[Path.GetFileNameWithoutExtension(path)] = con;
            return con;
        }


        /// <summary>
        /// sqliteConnect
        /// </summary>
        /// <param name="dbname"></param>
        /// <returns></returns>
        static public SQLiteConnection GetSqliteConnect(string dbname)
        {
            SqLiteConnectionMap.TryGetValue(dbname, out var con);
            return con;
        }

        /// <summary>
        /// 关闭
        /// </summary>
        static public void Close(string dbName = "")
        {
            if (string.IsNullOrEmpty(dbName))
            {
                Connection?.Dispose();
                Connection = null;
            }
            else
            {
                var ret = SqLiteConnectionMap.TryGetValue(dbName, out var con);
                if (ret)
                {
                    con.Dispose();
                    SqLiteConnectionMap.Remove(dbName);
                }
            }
        }


        /// <summary>
        /// 获取DB路径
        /// </summary>
        static public string GetLocalDBPath(string root, RuntimePlatform platform)
        {
            return IPath.Combine(root, BApplication.GetPlatformPath(platform), LOCAL_DB_PATH);
        }

        /// <summary>
        /// 获取DB路径
        /// </summary>
        static public string GetServerDBPath(string root, RuntimePlatform platform)
        {
            return IPath.Combine(root, BApplication.GetPlatformPath(platform), SERVER_DB_PATH);
        }

        #region Editor下加载

        /// <summary>
        /// 编辑器下加载DB，可读写|创建
        /// </summary>
        /// <param name="str"></param>
        static public string LoadLocalDBOnEditor(string root, RuntimePlatform platform)
        {
            //用当前平台目录进行加载
            var path = GetLocalDBPath(root, platform);
            LoadSQLOnEditor(path);

            return path;
        }


        /// <summary>
        /// 编辑器下加载DB，可读写|创建
        /// </summary>
        /// <param name="str"></param>
        static public void LoadServerDBOnEditor(string root, RuntimePlatform platform)
        {
            //用当前平台目录进行加载
            var path = GetServerDBPath(root, platform);
            LoadSQLOnEditor(path);
        }

        /// <summary>
        /// 加载Sql
        /// </summary>
        /// <param name="sqlPath"></param>
        static public void LoadSQLOnEditor(string sqlPath)
        {
            //
            Connection?.Dispose();
            //
            var dir = Path.GetDirectoryName(sqlPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            //编辑器下打开
            if (Application.isEditor)
            {
                //editor下 不在执行的时候，直接创建
                Connection = LoadDBReadWriteCreate(sqlPath);
                BDebug.Log("DB加载路径:" + sqlPath, "red");
            }
        }

        /// <summary>
        /// 删除数据库
        /// </summary>
        /// <param name="root"></param>
        /// <param name="platform"></param>
        /// <returns></returns>
        static public string DeleteDBFile(string root, RuntimePlatform platform)
        {
            //用当前平台目录进行加载
            var path = GetLocalDBPath(root, platform);

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return path;
        }

        #endregion
    }

    /// <summary>
    /// Sqlite辅助类
    /// </summary>
    static public class SqliteHelper
    {
        /// <summary>
        /// sqlite服务
        /// </summary>
        public class SQLiteService
        {
            //db connect
            public SQLiteConnection Connection { get; private set; }


            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="con"></param>
            public SQLiteService(SQLiteConnection con)
            {
                this.Connection = con;
                this.tableRuntime = new TableQueryForILRuntime(this.Connection);
            }

            /// <summary>
            /// 是否关闭
            /// </summary>
            public bool IsClose
            {
                get { return Connection == null || !Connection.IsOpen; }
            }

            /// <summary>
            /// DB路径
            /// </summary>
            public string DBPath
            {
                get { return this.Connection.DatabasePath; }
            }

            #region 常见的表格操作

            /// <summary>
            /// 创建db
            /// </summary>
            /// <typeparam name="T"></typeparam>
            public void CreateTable<T>()
            {
                Connection.DropTable<T>();
                Connection.CreateTable<T>();
            }

            /// <summary>
            /// 创建db
            /// </summary>
            /// <param name="t"></param>
            public void CreateTable(Type t)
            {
                Connection.DropTable(t);
                Connection.CreateTable(t);
            }

            /// <summary>
            /// 插入数据
            /// </summary>
            /// <param name="objects"></param>
            public void InsertTable(System.Collections.IEnumerable objects)
            {
                Connection.InsertAll(objects);
            }

            /// <summary>
            /// 插入数据
            /// </summary>
            /// <param name="objects"></param>
            public void Insert(object @object)
            {
                Connection.Insert(@object);
            }

            /// <summary>
            /// 插入所有
            /// </summary>
            /// <param name="obj"></param>
            /// <param name="objTypes"></param>
            public void InsertAll<T>(List<T> obj)
            {
                Connection.Insert(@obj, typeof(T));
            }

            /// <summary>
            /// 获取表
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <returns></returns>
            public TableQuery<T> GetTable<T>() where T : new()
            {
                return new TableQuery<T>(Connection);
            }

            #endregion

            #region 二次封装的表格操作 for ILRuntime

            private TableQueryForILRuntime tableRuntime;

            /// <summary>
            /// 获取TableRuntime
            /// </summary>
            public TableQueryForILRuntime TableRuntime
            {
                get { return tableRuntime; }
            }

            /// <summary>
            /// 获取TableRuntime
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <returns></returns>
            public TableQueryForILRuntime GetTableRuntime()
            {
                return tableRuntime;
            }

            #endregion
        }

        /// <summary>
        /// db服务
        /// </summary>
        static private SQLiteService dbservice;

        /// <summary>
        /// 获取主DB
        /// </summary>
        static public SQLiteService DB
        {
            get
            {
                if (dbservice == null || dbservice.IsClose)
                {
                    dbservice = new SQLiteService(SqliteLoder.Connection);
                }

                return dbservice;
            }
        }

        private static Dictionary<string, SQLiteService> DBServiceMap = new Dictionary<string, SQLiteService>();

        /// <summary>
        /// 获取一个DB
        /// </summary>
        /// <param name="dbName"></param>
        /// <returns></returns>
        static public SQLiteService GetDB(string dbName)
        {
            SQLiteService db = null;
            if (!DBServiceMap.TryGetValue(dbName, out db))
            {
                var con = SqliteLoder.GetSqliteConnect(dbName);
                db = new SQLiteService(con);
                DBServiceMap[dbName] = db;
            }

            return db;
        }


        /// <summary>
        /// 设置sql 缓存触发参数
        /// </summary>
        /// <param name="triggerCacheNum"></param>
        /// <param name="triggerChacheTimer"></param>
        static public void SetSqlCacheTrigger(int triggerCacheNum = 5, float triggerChacheTimer = 0.05f)
        {
            DB.TableRuntime.EnableSqlCahce(triggerCacheNum, triggerChacheTimer);
        }

        #region ILRuntime 重定向

        /// <summary>
        /// 注册SqliteHelper的ILR重定向
        /// </summary>
        /// <param name="appdomain"></param>
        public unsafe static void RegisterCLRRedirection(ILRuntime.Runtime.Enviorment.AppDomain appdomain)
        {
            //from all
            foreach (var mi in typeof(TableQueryForILRuntime).GetMethods())
            {
                if (mi.Name == "FromAll" && mi.IsGenericMethodDefinition && mi.GetParameters().Length == 1)
                {
                    appdomain.RegisterCLRMethodRedirection(mi, RedirFromAll);
                }
                else if (mi.Name == "From" && mi.IsGenericMethodDefinition && mi.GetParameters().Length == 1)
                {
                    appdomain.RegisterCLRMethodRedirection(mi, RedirFrom);
                }
            }
        }

        /// <summary>
        /// FromAll的重定向
        /// </summary>
        /// <param name="intp"></param>
        /// <param name="esp"></param>
        /// <param name="mStack"></param>
        /// <param name="method"></param>
        /// <param name="isNewObj"></param>
        /// <returns></returns>
        public unsafe static StackObject* RedirFromAll(ILIntepreter intp, StackObject* esp, IList<object> mStack,
            CLRMethod method, bool isNewObj)
        {
            ILRuntime.Runtime.Enviorment.AppDomain __domain = intp.AppDomain;
            StackObject* ptr_of_this_method;
            StackObject* __ret = ILIntepreter.Minus(esp, 1);
            ptr_of_this_method = ILIntepreter.Minus(esp, 1);
            //
            System.String selection =
                (System.String)typeof(System.String).CheckCLRTypes(StackObject.ToObject(ptr_of_this_method, __domain,
                    mStack));
            intp.Free(ptr_of_this_method);
            var generic = method.GenericArguments[0];
            //调用
            var result_of_this_method = DB.GetTableRuntime().FromAll(generic.ReflectionType, selection);

            if (generic is CLRType)
            {
                // 创建clrTypeInstance
                var clrType = generic.TypeForCLR;
                var genericType = typeof(List<>).MakeGenericType(clrType);
                var retList = (IList)Activator.CreateInstance(genericType);

                for (int i = 0; i < result_of_this_method.Count; i++)
                {
                    var obj = result_of_this_method[i];
                    retList.Add(obj);
                }

                return ILIntepreter.PushObject(__ret, mStack, retList);
            }
            else
            {
                // 转成ilrTypeInstance
                var retList = new List<ILTypeInstance>(result_of_this_method.Count);
                for (int i = 0; i < result_of_this_method.Count; i++)
                {
                    var hotfixObj = result_of_this_method[i] as ILTypeInstance;
                    retList.Add(hotfixObj);
                }

                return ILIntepreter.PushObject(__ret, mStack, retList);
            }
        }


        /// <summary>
        /// From重定向
        /// </summary>
        /// <param name="intp"></param>
        /// <param name="esp"></param>
        /// <param name="mStack"></param>
        /// <param name="method"></param>
        /// <param name="isNewObj"></param>
        /// <returns></returns>
        public unsafe static StackObject* RedirFrom(ILIntepreter intp, StackObject* esp, IList<object> mStack,
            CLRMethod method, bool isNewObj)
        {
            ILRuntime.Runtime.Enviorment.AppDomain __domain = intp.AppDomain;
            StackObject* ptr_of_this_method;
            StackObject* __ret = ILIntepreter.Minus(esp, 1);
            ptr_of_this_method = ILIntepreter.Minus(esp, 1);
            //
            System.String selection =
                (System.String)typeof(System.String).CheckCLRTypes(StackObject.ToObject(ptr_of_this_method, __domain,
                    mStack));
            intp.Free(ptr_of_this_method);
            var generic = method.GenericArguments[0];
            //调用
            var result_of_this_method = DB.GetTableRuntime().From(generic.ReflectionType, selection);

            // if (generic is CLRType)
            // {
            return ILIntepreter.PushObject(__ret, mStack, result_of_this_method);
            // }
            // else
            // {
            //     // 转成ilrTypeInstance
            //
            //     var ilrInstance = result_of_this_method as ILTypeInstance;
            //     return ILIntepreter.PushObject(__ret, mStack, ilrInstance);
            // }
        }

        #endregion
    }
}