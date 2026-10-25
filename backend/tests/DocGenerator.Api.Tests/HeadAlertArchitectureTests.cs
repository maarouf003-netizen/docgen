using System.Reflection;
using System.Reflection.Emit;

using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Outbox;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حارس معماري للنمط المركزي (الخطة §8.1): لا يُنشأ <see cref="HeadAlert"/> ولا يُضاف
/// عبر المستودع ولا تُستدعى معالجة النظام إلا من المالكين المصرّح لهم. يُفحص الـIL
/// للتجميعات المترجمة — لا النص المصدري — حتى لا يفلت مسار غير مباشر (مثل حالة
/// <c>async</c> داخل آلة حالة). التجميعات الاختبارية مستثناة عمدًا.
/// </summary>
public class HeadAlertArchitectureTests
{
    private static readonly HashSet<string> AllowedOwners = new(StringComparer.Ordinal)
    {
        typeof(HeadAlertService).FullName!,
        typeof(HeadAlertOutboxDispatcher).FullName!,
    };

    private static readonly Dictionary<ushort, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opCode => unchecked((ushort)opCode.Value));

    [Fact]
    public void HeadAlert_NoDirectConstructionOutsideService()
    {
        var assemblies = new[]
        {
            typeof(HeadAlert).Assembly,
            typeof(HeadAlertService).Assembly,
            typeof(HeadAlertOutboxDispatcher).Assembly,
            typeof(global::Program).Assembly,
        }.Distinct();

        var violations = new List<string>();
        var sawConstruction = false;
        var sawRepositoryAdd = false;
        var sawSystemProcessing = false;

        foreach (var assembly in assemblies)
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                foreach (var method in type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    var body = method.GetMethodBody();
                    var il = body?.GetILAsByteArray();
                    if (il is null || il.Length == 0)
                        continue;

                    foreach (var (opCode, token) in ReadInstructions(il))
                    {
                        if (opCode.OperandType != OperandType.InlineMethod)
                            continue;

                        MethodBase? target;
                        try
                        {
                            target = method.Module.ResolveMethod(
                                token,
                                type.IsGenericType ? type.GetGenericArguments() : null,
                                method.IsGenericMethod ? method.GetGenericArguments() : null);
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }

                        if (target is null)
                            continue;

                        var constructsAlert = opCode == OpCodes.Newobj
                            && target is ConstructorInfo
                            && target.DeclaringType == typeof(HeadAlert);
                        var addsAlert = IsHeadAlertAddAsync(target);
                        var processesSystemAlert = IsCreateSystemAsync(target);

                        if (!constructsAlert && !addsAlert && !processesSystemAlert)
                            continue;

                        var owner = type;
                        while (owner.DeclaringType is not null)
                            owner = owner.DeclaringType;

                        var ownerName = owner.FullName ?? owner.Name;
                        if (AllowedOwners.Contains(ownerName))
                        {
                            sawConstruction |= constructsAlert;
                            sawRepositoryAdd |= addsAlert;
                            sawSystemProcessing |= processesSystemAlert;
                            continue;
                        }

                        var operation = constructsAlert
                            ? "new HeadAlert"
                            : addsAlert
                                ? "IHeadAlertRepository.AddAsync"
                                : "IHeadAlertService.CreateSystemAsync";
                        violations.Add($"{ownerName}.{method.Name} → {operation}");
                    }
                }
            }
        }

        Assert.Empty(violations);

        // إثبات أن الفحص رأى المسارات المشروعة فعلًا، لا أنه مرّ على IL فارغ.
        Assert.True(sawConstruction, "لم يُرصد إنشاء HeadAlert في الخدمة — الفحص لم يقرأ الـIL كما ينبغي.");
        Assert.True(sawRepositoryAdd, "لم يُرصد AddAsync للمستودع في الخدمة — الفحص لم يقرأ الـIL كما ينبغي.");
        Assert.True(sawSystemProcessing, "لم يُرصد استدعاء CreateSystemAsync في المشغّل — الفحص لم يقرأ الـIL كما ينبغي.");
    }

    private static bool IsHeadAlertAddAsync(MethodBase target)
    {
        if (!string.Equals(target.Name, "AddAsync", StringComparison.Ordinal))
            return false;

        var parameters = target.GetParameters();
        return parameters.Length >= 1 && parameters[0].ParameterType == typeof(HeadAlert);
    }

    private static bool IsCreateSystemAsync(MethodBase target)
    {
        if (!string.Equals(target.Name, "CreateSystemAsync", StringComparison.Ordinal))
            return false;

        var parameters = target.GetParameters();
        return parameters.Length >= 1
            && parameters[0].ParameterType == typeof(CreateSystemHeadAlertRequest);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }

    private static IEnumerable<(OpCode OpCode, int Operand)> ReadInstructions(byte[] il)
    {
        var offset = 0;
        while (offset < il.Length)
        {
            OpCode opCode;
            var first = il[offset++];
            if (first == 0xFE)
            {
                if (offset >= il.Length)
                    yield break;
                opCode = OpCodesByValue[unchecked((ushort)(0xFE00 | il[offset++]))];
            }
            else
            {
                opCode = OpCodesByValue[first];
            }

            // `switch` وحده ذو معامل متغيّر الطول: عدد الحالات (4 بايت) ثم إزاحاتها.
            if (opCode.OperandType == OperandType.InlineSwitch)
            {
                if (offset + sizeof(int) > il.Length)
                    yield break;
                var count = BitConverter.ToInt32(il, offset);
                offset += sizeof(int);
                var switchBytes = (long)count * sizeof(int);
                if (switchBytes < 0 || offset + switchBytes > il.Length)
                    yield break;
                offset += (int)switchBytes;
                yield return (opCode, 0);
                continue;
            }

            var operandSize = OperandSize(opCode.OperandType);
            var operand = 0;
            if (operandSize == 4 && offset + sizeof(int) <= il.Length)
                operand = BitConverter.ToInt32(il, offset);

            offset += operandSize;
            yield return (opCode, operand);
        }
    }

    private static int OperandSize(OperandType operandType) => operandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineField
            or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString
            or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        _ => 0,
    };
}
