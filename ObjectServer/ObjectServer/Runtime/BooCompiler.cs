﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using System.Reflection;
using Boo.Lang.Compiler;
using Boo.Lang.Compiler.IO;
using Boo.Lang.Compiler.Pipelines;
using Boo.Lang.Parser;


namespace ObjectServer.Runtime
{
    /// <summary>
    /// Boo 语言代码编译器，用于把模块的代码编译成内存中的 Assembly
    /// </summary>
    internal class BooCompiler : ICompiler
    {
        #region ICompiler 成员

        public Assembly CompileFromFile(IEnumerable<string> sourceFiles)
        {
            var compiler = new Boo.Lang.Compiler.BooCompiler();
            var coreAssembly = typeof(Model.TableModel).Assembly;
            compiler.Parameters.Pipeline = new CompileToMemory();
            compiler.Parameters.Ducky = true;
            compiler.Parameters.WarnAsError = false;

            compiler.Parameters.AddAssembly(coreAssembly);
            compiler.Parameters.AddAssembly(typeof(log4net.ILog).Assembly);
            var t1 = typeof(Boo.Lang.Parser.BooParser);//WORKAROUND

            foreach (var source in sourceFiles)
            {
                compiler.Parameters.Input.Add(new FileInput(source));
            }

            CompilerContext context = compiler.Run();

            LogWarnings(context);

            //编译失败
            if (context.GeneratedAssembly == null)
            {
                LogErrors(context);
                //TODO 编译错误类型转换
                //throw new CompileException("Failed to compile module", context.Errors);
                throw new ArgumentException("Failed to compile module");
            }

            return context.GeneratedAssembly;
        }

        private static void LogWarnings(CompilerContext context)
        {

            foreach (var w in context.Warnings)
            {
                Logger.Warn(() => w.Message);
            }
        }

        private static void LogErrors(CompilerContext context)
        {
            foreach (CompilerError error in context.Errors)
            {
                Logger.Error(() => error.Message);
            }
        }

        #endregion
    }
}
