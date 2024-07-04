using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintFunctionExpression : JintExpression
    {
        private readonly JintFunctionDefinition _function;

        public JintFunctionExpression(FunctionExpression function) : base(function)
        {
            _function = new JintFunctionDefinition(function);
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            return GetValue(context);
        }

        public override JsValue GetValue(EvaluationContext context)
        {
            ScriptFunction closure;
            var functionName = (Key) (_function.Name ?? "");
            if (!_function.Function.Generator)
            {
                closure = _function.Function.Async
                    ? InstantiateAsyncFunctionExpression(context, functionName)
                    : InstantiateOrdinaryFunctionExpression(context, functionName);
            }
            else
            {
                closure = InstantiateGeneratorFunctionExpression(context, functionName);
            }

            return closure;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiateordinaryfunctionexpression
        /// </summary>
        private ScriptFunction InstantiateOrdinaryFunctionExpression(EvaluationContext context, Key name)
        {
            var engine = context.Engine;
            var runningExecutionContext = engine.ExecutionContext;
            var scope = runningExecutionContext.LexicalEnvironment;

            DeclarativeEnvironment? funcEnv = null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                funcEnv = JintEnvironment.NewDeclarativeEnvironment(engine, engine.ExecutionContext.LexicalEnvironment);
                funcEnv.CreateImmutableBinding(name, strict: false);
            }

            var privateEnv = runningExecutionContext.PrivateEnvironment;

            var thisMode = _function.Strict
                ? FunctionThisMode.Strict
                : FunctionThisMode.Global;

            var intrinsics = engine.Realm.Intrinsics;
            var closure = intrinsics.Function.OrdinaryFunctionCreate(
                intrinsics.Function.PrototypeObject,
                _function,
                thisMode,
                funcEnv ?? scope,
                privateEnv
            );

            closure.SetFunctionName(JsString.Create(name));

            closure.MakeConstructor();

            funcEnv?.InitializeBinding(name, closure);

            return closure;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiateasyncfunctionexpression
        /// </summary>
        private ScriptFunction InstantiateAsyncFunctionExpression(EvaluationContext context, Key name)
        {
            var engine = context.Engine;
            var runningExecutionContext = engine.ExecutionContext;
            var scope = runningExecutionContext.LexicalEnvironment;

            DeclarativeEnvironment? funcEnv = null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                funcEnv = JintEnvironment.NewDeclarativeEnvironment(engine, engine.ExecutionContext.LexicalEnvironment);
                funcEnv.CreateImmutableBinding(name, strict: false);
            }

            var privateScope = runningExecutionContext.PrivateEnvironment;

            var thisMode = _function.Strict
                ? FunctionThisMode.Strict
                : FunctionThisMode.Global;

            var intrinsics = engine.Realm.Intrinsics;
            var closure = intrinsics.Function.OrdinaryFunctionCreate(
                intrinsics.AsyncFunction.PrototypeObject,
                _function,
                thisMode,
                funcEnv ?? scope,
                privateScope
            );

            closure.SetFunctionName(name.Name);

            funcEnv?.InitializeBinding(name, closure);

            return closure;
        }


        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiategeneratorfunctionexpression
        /// </summary>
        private ScriptFunction InstantiateGeneratorFunctionExpression(EvaluationContext context, Key name)
        {
            var engine = context.Engine;
            var runningExecutionContext = engine.ExecutionContext;
            var scope = runningExecutionContext.LexicalEnvironment;

            DeclarativeEnvironment? funcEnv = null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                funcEnv = JintEnvironment.NewDeclarativeEnvironment(engine, engine.ExecutionContext.LexicalEnvironment);
                funcEnv.CreateImmutableBinding(name, strict: false);
            }

            var privateScope = runningExecutionContext.PrivateEnvironment;

            var thisMode = _function.Strict || engine._isStrict
                ? FunctionThisMode.Strict
                : FunctionThisMode.Global;

            var intrinsics = engine.Realm.Intrinsics;
            var closure = intrinsics.Function.OrdinaryFunctionCreate(
                intrinsics.GeneratorFunction.PrototypeObject,
                _function,
                thisMode,
                funcEnv ?? scope,
                privateScope
            );

            closure.SetFunctionName(name.Name);

            var prototype = ObjectInstance.OrdinaryObjectCreate(engine, intrinsics.GeneratorFunction.PrototypeObject.PrototypeObject);
            closure.DefinePropertyOrThrow(CommonProperties.Prototype, new PropertyDescriptor(prototype, PropertyFlag.Writable));

            funcEnv?.InitializeBinding(name!, closure);

            return closure;
        }
    }
}
