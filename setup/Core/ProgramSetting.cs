﻿using System.Linq.Expressions;
using Terraria.ModLoader.Setup.Core.Abstractions;

namespace Terraria.ModLoader.Setup.Core;

public class ProgramSetting<T>
{
	private readonly Func<ProgramSettings, T> getter;
	private readonly Action<ProgramSettings,T> setter;
	private readonly ProgramSettings programSettings;

	public ProgramSetting(Expression<Func<ProgramSettings, T>> expression, ProgramSettings programSettings)
	{
		this.getter = expression.Compile();
		this.setter = MakeSetter(expression).Compile();
		this.programSettings = programSettings;
	}

	public void Set(T value)
	{
		setter(programSettings, value);
		programSettings.Save();
	}

	public T Get()
	{
		return getter(programSettings);
	}

	private static Expression<Action<ProgramSettings, T>> MakeSetter(Expression<Func<ProgramSettings, T>> getter)
	{
		var memberExpr = (MemberExpression)getter.Body;
		var @this = Expression.Parameter(typeof(ProgramSettings), "$this");
		var value = Expression.Parameter(typeof(T), "value");
		return Expression.Lambda<Action<ProgramSettings, T>>(
			Expression.Assign(Expression.MakeMemberAccess(@this, memberExpr.Member), value),
			@this, value);
	}
}