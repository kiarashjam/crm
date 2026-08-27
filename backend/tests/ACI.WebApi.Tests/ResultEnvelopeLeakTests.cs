using System.Reflection;
using ACI.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Tests;

/// <summary>
/// No endpoint may serialise the <see cref="Result{T}"/> wrapper instead of its value.
/// </summary>
/// <remarks>
/// <para>
/// <c>Ok(result)</c> compiles perfectly, returns 200, and produces
/// <c>{"isSuccess":true,"isFailure":false,"error":{...},"value":[...]}</c> — an
/// object where the client expects the value. The frontend's guard
/// (<c>Array.isArray(list) ? list : []</c>) then reads every response as an empty
/// list and carries on quietly.
/// </para>
/// <para>
/// That is what stopped pipeline steps from moving a lead's status. Nothing failed
/// anywhere: the request was a 200, the body was valid JSON, and the status write
/// was held back because the status list looked empty. Three of the four
/// org-config controllers used <c>ToActionResult()</c>; only LeadStatuses used
/// <c>Ok()</c>, so only the status was affected — and the difference is one word
/// at one call site, which is exactly the kind of thing that comes back.
/// </para>
/// <para>
/// It also made the not-found paths dead: <c>if (status == null) return
/// NotFound()</c> can never fire, because <c>Result&lt;T&gt;</c> is a class and is
/// never null. A missing status answered 200 with a failure envelope inside.
/// </para>
/// <para>
/// This reads the compiled IL rather than the source, so a rename, a reformat, or
/// a helper wrapper cannot hide a leak from it.
/// </para>
/// </remarks>
public class ResultEnvelopeLeakTests
{
    private static bool IsResultType(Type? t)
    {
        while (t is not null && t != typeof(object))
        {
            if (t == typeof(Result)) return true;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Result<>)) return true;
            t = t.BaseType;
        }
        return false;
    }

    /// <summary>Unwraps Task&lt;T&gt;, ActionResult&lt;T&gt; and Result&lt;T&gt; one layer at a time.</summary>
    private static IEnumerable<Type> Unwrap(Type t)
    {
        yield return t;
        while (t.IsGenericType)
        {
            var arg = t.GetGenericArguments()[0];
            yield return arg;
            t = arg;
        }
    }

    [Fact]
    public void NoActionDeclaresAResultTypeAsItsResponseBody()
    {
        // The signature is the cheap half: an action typed
        // Task<ActionResult<Result<T>>> is leaking by declaration.
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToArray();

        controllers.Should().NotBeEmpty("the WebApi assembly should contain controllers");

        var leaking = new List<string>();
        foreach (var controller in controllers)
        {
            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (action.IsSpecialName) continue;
                if (Unwrap(action.ReturnType).Any(IsResultType))
                {
                    leaking.Add($"{controller.Name}.{action.Name}");
                }
            }
        }

        leaking.Should().BeEmpty("an action must return the VALUE, not the Result wrapper");
    }

    [Fact]
    public void NoActionPassesAResultStraightToOk()
    {
        // The half that actually caught this. The declared return type was already
        // ActionResult<IReadOnlyList<LeadStatusDto>> — correct — while the body
        // handed Ok() a Result<IReadOnlyList<LeadStatusDto>>. ActionResult<T> takes
        // an implicit object, so nothing complained.
        //
        // So: for every call to a one-argument ControllerBase.Ok, look at what was
        // loaded immediately before it and flag the call if that value is a Result.
        // Reading the ARGUMENT rather than merely "this method mentions a Result
        // somewhere" is what separates the real leak from the several honest methods
        // that hold a Result to test IsSuccess and then return Ok(somethingElse).
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToArray();

        controllers.Should().NotBeEmpty("the WebApi assembly should contain controllers");

        var leaking = new List<string>();
        var scanned = 0;

        foreach (var controller in controllers)
        {
            // An async action's body lives in a compiler-generated state machine, so
            // that is where the call actually is.
            var bodies = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Select(m => (Owner: (Type)controller, Name: m.Name, Method: (MethodBase)m))
                .Concat(controller
                    .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(n => (
                        Owner: n,
                        Name: ActionNameOf(n),
                        Method: (MethodBase?)n.GetMethod(
                            "MoveNext",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)))
                    .Where(x => x.Method is not null)
                    .Select(x => (x.Owner, x.Name, Method: x.Method!)));

            foreach (var (owner, name, method) in bodies)
            {
                var body = method.GetMethodBody();
                var il = body?.GetILAsByteArray();
                if (body is null || il is null) continue;
                scanned++;

                if (LeaksAResultToOk(il, method.Module, body, owner))
                {
                    leaking.Add($"{controller.Name}.{name}");
                }
            }
        }

        scanned.Should().BeGreaterThan(40, "the scan should be reaching real action bodies");
        leaking.Should().BeEmpty(
            "passing a Result to Ok() serialises the wrapper — "
            + "{\"isSuccess\":true,\"value\":[...]} — where the client expects the value; "
            + "use result.ToActionResult() so the value is returned and a failure "
            + "becomes a real status code");
    }

    /// <summary>
    /// Whether the IL contains <c>ld… &lt;a Result&gt;</c> immediately followed by a
    /// call to a one-argument <c>ControllerBase.Ok</c>.
    /// </summary>
    private static bool LeaksAResultToOk(byte[] il, Module module, MethodBody body, Type owner)
    {
        for (var i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != 0x28 && il[i] != 0x6F) continue;  // call / callvirt

            MethodBase? target;
            try
            {
                target = module.ResolveMethod(BitConverter.ToInt32(il, i + 1));
            }
            catch (ArgumentException)
            {
                // Not a method token: this byte was operand data, not an opcode.
                // Scanning without decoding instruction lengths costs the occasional
                // bad token, and every real call site is still visited.
                continue;
            }

            if (target?.Name != "Ok" || target.DeclaringType != typeof(ControllerBase)) continue;
            if (target.GetParameters().Length == 0) continue;  // Ok() carries no body

            if (IsResultType(LoadedBefore(il, i, module, body, owner))) return true;
        }
        return false;
    }

    /// <summary>
    /// The type of the value the instruction ending at <paramref name="callAt"/> loaded,
    /// for the load forms the C# compiler actually emits before a call like this.
    /// </summary>
    private static Type? LoadedBefore(byte[] il, int callAt, Module module, MethodBody body, Type owner)
    {
        // ldfld <token>  — the awaited value in an async state machine
        if (callAt >= 5 && il[callAt - 5] == 0x7B)
        {
            try
            {
                return module.ResolveField(BitConverter.ToInt32(il, callAt - 4))?.FieldType;
            }
            catch (ArgumentException) { return null; }
        }

        var locals = body.LocalVariables;
        Type? Local(int index) => index >= 0 && index < locals.Count ? locals[index].LocalType : null;

        // ldloc.0 … ldloc.3
        if (callAt >= 1 && il[callAt - 1] >= 0x06 && il[callAt - 1] <= 0x09) return Local(il[callAt - 1] - 0x06);
        // ldloc.s <n>
        if (callAt >= 2 && il[callAt - 2] == 0x11) return Local(il[callAt - 1]);
        // ldloc <n>
        if (callAt >= 4 && il[callAt - 4] == 0xFE && il[callAt - 3] == 0x0C) {
            return Local(BitConverter.ToUInt16(il, callAt - 2));
        }
        return null;
    }

    /// <summary>"&lt;GetLeadStatuses&gt;d__4" -&gt; "GetLeadStatuses".</summary>
    private static string ActionNameOf(Type stateMachine)
    {
        var name = stateMachine.Name;
        var start = name.IndexOf('<');
        var end = name.IndexOf('>');
        return start >= 0 && end > start ? name[(start + 1)..end] : name;
    }
}
