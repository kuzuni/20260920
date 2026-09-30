using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace BACKND.Database.Internal
{
    /// <summary>
    /// Expression Tree를 분석하여 WhereCondition으로 변환하는 유틸리티
    /// </summary>
    public class ExpressionAnalyzer
    {
        private readonly BaseModel modelInstance;

        public ExpressionAnalyzer(BaseModel modelInstance)
        {
            this.modelInstance = modelInstance;
        }

        /// <summary>
        /// Expression을 분석하여 WhereCondition 목록 반환
        /// </summary>
        public List<WhereCondition> Analyze<T>(Expression<Func<T, bool>> expression)
        {
            var additionalConditions = new List<WhereCondition>();

            var result = AnalyzeBinaryExpression(expression.Body, additionalConditions);

            var conditions = new List<WhereCondition>(additionalConditions);
            if (result != null)
            {
                conditions.Add(result);
            }
            return conditions;
        }

        /// <summary>
        /// 단일 Expression 분석 (QueryBuilder 호환용)
        /// </summary>
        public WhereCondition AnalyzeSingle<T>(Expression<Func<T, bool>> expression, List<WhereCondition> targetList)
        {
            var additionalConditions = new List<WhereCondition>();

            var result = AnalyzeBinaryExpression(expression.Body, additionalConditions);

            foreach (var condition in additionalConditions)
            {
                targetList.Add(condition);
            }

            return result;
        }

        private WhereCondition AnalyzeBinaryExpression(Expression expression, List<WhereCondition> additionalConditions)
        {
            if (expression is BinaryExpression binaryExpr)
            {
                if (binaryExpr.NodeType == ExpressionType.AndAlso || binaryExpr.NodeType == ExpressionType.OrElse)
                {
                    var leftCondition = AnalyzeBinaryExpression(binaryExpr.Left, additionalConditions);
                    var rightCondition = AnalyzeBinaryExpression(binaryExpr.Right, additionalConditions);

                    if (leftCondition != null && rightCondition != null)
                    {
                        if (binaryExpr.NodeType == ExpressionType.OrElse)
                        {
                            rightCondition.LogicalOperator = LogicalOperator.Or;
                            leftCondition.IsGroupStart = true;
                            rightCondition.IsGroupEnd = true;
                        }

                        additionalConditions.Add(leftCondition);
                        return rightCondition;
                    }
                }

                if (IsComparisonOperator(binaryExpr.NodeType))
                {
                    var columnName = GetColumnNameFromMemberExpression(binaryExpr.Left);
                    var value = GetConstantValue(binaryExpr.Right);
                    var op = GetCompareOperator(binaryExpr.NodeType);

                    return new WhereCondition
                    {
                        ColumnName = columnName,
                        Operator = op,
                        Value = value,
                        LogicalOperator = LogicalOperator.And
                    };
                }

                throw new NotSupportedException($"Binary operator {binaryExpr.NodeType} not yet supported");
            }

            if (expression is MethodCallExpression methodCall)
            {
                return AnalyzeMethodCall(methodCall);
            }

            if (expression is UnaryExpression unaryExpr)
            {
                if (unaryExpr.NodeType == ExpressionType.Not)
                {
                    var innerCondition = AnalyzeBinaryExpression(unaryExpr.Operand, additionalConditions);
                    if (innerCondition != null)
                    {
                        innerCondition.Operator = GetNegatedOperator(innerCondition.Operator);
                        return innerCondition;
                    }
                }
            }

            throw new NotSupportedException($"Expression type {expression.NodeType} is not supported");
        }

        private WhereCondition AnalyzeMethodCall(MethodCallExpression methodCall)
        {
            if (methodCall.Method.Name == "Contains" && methodCall.Method.DeclaringType == typeof(string))
            {
                throw new NotSupportedException("String.Contains is not supported because server doesn't support LIKE operator. Use exact equality comparison instead.");
            }

            if (methodCall.Method.Name == "Contains" && methodCall.Object != null)
            {
                var columnName = GetColumnNameFromMemberExpression(methodCall.Arguments[0]);
                var values = GetConstantValue(methodCall.Object);

                return new WhereCondition
                {
                    ColumnName = columnName,
                    Operator = CompareOperator.In,
                    Value = values,
                    LogicalOperator = LogicalOperator.And
                };
            }

            throw new NotSupportedException($"Method {methodCall.Method.Name} is not supported");
        }

        /// <summary>
        /// MemberExpression에서 컬럼명 추출
        /// </summary>
        public string GetColumnNameFromMemberExpression(Expression expression)
        {
            if (expression is UnaryExpression unaryExpr &&
                (unaryExpr.NodeType == ExpressionType.Convert || unaryExpr.NodeType == ExpressionType.ConvertChecked))
            {
                expression = unaryExpr.Operand;
            }

            if (expression is MemberExpression memberExpr && memberExpr.Expression?.NodeType == ExpressionType.Parameter)
            {
                return modelInstance.GetColumnName(memberExpr.Member.Name);
            }

            throw new NotSupportedException("Only simple property access is supported (e.g., x => x.PropertyName)");
        }

        /// <summary>
        /// KeySelector Expression에서 컬럼명 추출
        /// </summary>
        public string GetColumnNameFromKeySelector<T, TKey>(Expression<Func<T, TKey>> keySelector)
        {
            return GetColumnNameFromMemberExpression(keySelector.Body);
        }

        /// <summary>
        /// Expression에서 상수 값 추출
        /// </summary>
        public static object GetConstantValue(Expression expression)
        {
            if (expression is ConstantExpression constantExpr)
            {
                return constantExpr.Value;
            }

            if (expression is MemberExpression memberExpr)
            {
                if (memberExpr.Expression is ConstantExpression containerExpr)
                {
                    var container = containerExpr.Value;
                    if (container == null) return null;

                    if (memberExpr.Member is System.Reflection.FieldInfo field)
                    {
                        return field.GetValue(container);
                    }
                    if (memberExpr.Member is System.Reflection.PropertyInfo property)
                    {
                        return property.GetValue(container);
                    }
                }

                if (memberExpr.Expression == null && memberExpr.Member is System.Reflection.FieldInfo staticField)
                {
                    return staticField.GetValue(null);
                }
                if (memberExpr.Expression == null && memberExpr.Member is System.Reflection.PropertyInfo staticProperty)
                {
                    return staticProperty.GetValue(null);
                }

                if (memberExpr.Expression is MemberExpression nestedMember)
                {
                    var parentValue = GetConstantValue(nestedMember);
                    if (parentValue == null) return null;

                    if (memberExpr.Member is System.Reflection.FieldInfo field)
                    {
                        return field.GetValue(parentValue);
                    }
                    if (memberExpr.Member is System.Reflection.PropertyInfo property)
                    {
                        return property.GetValue(parentValue);
                    }
                }
            }

            if (expression is UnaryExpression unaryExpr)
            {
                if (unaryExpr.NodeType == ExpressionType.Convert || unaryExpr.NodeType == ExpressionType.ConvertChecked)
                {
                    var innerValue = GetConstantValue(unaryExpr.Operand);
                    if (innerValue != null && innerValue.GetType().IsEnum)
                    {
                        return Convert.ChangeType(innerValue, Enum.GetUnderlyingType(innerValue.GetType()));
                    }
                    return innerValue;
                }

                if (unaryExpr.NodeType == ExpressionType.Quote)
                {
                    return GetConstantValue(unaryExpr.Operand);
                }
            }

            if (expression is MethodCallExpression methodCallExpr)
            {
                try
                {
                    if (methodCallExpr.Object == null)
                    {
                        var args = methodCallExpr.Arguments.Select(GetConstantValue).ToArray();
                        return methodCallExpr.Method.Invoke(null, args);
                    }

                    var instance = GetConstantValue(methodCallExpr.Object);
                    if (instance != null)
                    {
                        var args = methodCallExpr.Arguments.Select(GetConstantValue).ToArray();
                        return methodCallExpr.Method.Invoke(instance, args);
                    }
                }
                catch (Exception ex)
                {
                    throw new NotSupportedException(
                        $"Failed to evaluate '{methodCallExpr.Method.Name}' in expression. " +
                        $"On IL2CPP (iOS/WebGL), some reflection operations are restricted. " +
                        $"Extract the value into a local variable before using it in Where(). " +
                        $"Example: var val = obj.{methodCallExpr.Method.Name}(...); Where(x => x.Field == val)", ex);
                }
            }

            if (expression is NewExpression newExpr)
            {
                try
                {
                    var args = newExpr.Arguments.Select(GetConstantValue).ToArray();
                    return Activator.CreateInstance(newExpr.Type, args);
                }
                catch (Exception ex)
                {
                    throw new NotSupportedException(
                        $"Failed to create instance of '{newExpr.Type.Name}' in expression. " +
                        $"On IL2CPP (iOS/WebGL), some constructors cannot be invoked via reflection. " +
                        $"Create the instance in a local variable before using it in Where(). " +
                        $"Example: var val = new {newExpr.Type.Name}(...); Where(x => x.Field == val)", ex);
                }
            }

            if (expression is ConditionalExpression conditionalExpr)
            {
                var testValue = GetConstantValue(conditionalExpr.Test);
                if (testValue is bool testBool)
                {
                    return testBool
                        ? GetConstantValue(conditionalExpr.IfTrue)
                        : GetConstantValue(conditionalExpr.IfFalse);
                }
            }

            if (expression is BinaryExpression binaryExpr && !IsComparisonOperator(binaryExpr.NodeType))
            {
                var left = GetConstantValue(binaryExpr.Left);
                var right = GetConstantValue(binaryExpr.Right);

                return binaryExpr.NodeType switch
                {
                    ExpressionType.Add => AddValues(left, right),
                    ExpressionType.Subtract => SubtractValues(left, right),
                    ExpressionType.Multiply => MultiplyValues(left, right),
                    ExpressionType.Divide => DivideValues(left, right),
                    ExpressionType.Modulo => ModuloValues(left, right),
                    ExpressionType.Coalesce => left ?? right,
                    _ => throw new NotSupportedException($"Binary operator {binaryExpr.NodeType} not supported in constant evaluation")
                };
            }

            throw new NotSupportedException(
                $"Expression {expression.NodeType} cannot be evaluated at compile time. " +
                "Use local variables: var id = 5; Where(x => x.Id == id)");
        }

        #region Helper Methods

        public static bool IsComparisonOperator(ExpressionType nodeType)
        {
            return nodeType == ExpressionType.Equal ||
                   nodeType == ExpressionType.NotEqual ||
                   nodeType == ExpressionType.GreaterThan ||
                   nodeType == ExpressionType.GreaterThanOrEqual ||
                   nodeType == ExpressionType.LessThan ||
                   nodeType == ExpressionType.LessThanOrEqual;
        }

        public static CompareOperator GetCompareOperator(ExpressionType nodeType)
        {
            return nodeType switch
            {
                ExpressionType.Equal => CompareOperator.Equal,
                ExpressionType.NotEqual => CompareOperator.NotEqual,
                ExpressionType.GreaterThan => CompareOperator.GreaterThan,
                ExpressionType.GreaterThanOrEqual => CompareOperator.GreaterThanOrEqual,
                ExpressionType.LessThan => CompareOperator.LessThan,
                ExpressionType.LessThanOrEqual => CompareOperator.LessThanOrEqual,
                _ => throw new NotSupportedException($"Operation {nodeType} is not supported")
            };
        }

        public static CompareOperator GetNegatedOperator(CompareOperator originalOperator)
        {
            return originalOperator switch
            {
                CompareOperator.Equal => CompareOperator.NotEqual,
                CompareOperator.NotEqual => CompareOperator.Equal,
                CompareOperator.GreaterThan => CompareOperator.LessThanOrEqual,
                CompareOperator.GreaterThanOrEqual => CompareOperator.LessThan,
                CompareOperator.LessThan => CompareOperator.GreaterThanOrEqual,
                CompareOperator.LessThanOrEqual => CompareOperator.GreaterThan,
                CompareOperator.IsNull => CompareOperator.IsNotNull,
                CompareOperator.IsNotNull => CompareOperator.IsNull,
                _ => throw new NotSupportedException($"Cannot negate operator {originalOperator}")
            };
        }

        #endregion

        #region Arithmetic Operations

        public static object AddValues(object left, object right)
        {
            if (left is int l1 && right is int r1) return l1 + r1;
            if (left is long l2 && right is long r2) return l2 + r2;
            if (left is float l3 && right is float r3) return l3 + r3;
            if (left is double l4 && right is double r4) return l4 + r4;
            if (left is string l5 && right is string r5) return l5 + r5;
            if (left is DateTime l6 && right is TimeSpan r6) return l6 + r6;
            throw new NotSupportedException($"Cannot add {left?.GetType().Name} and {right?.GetType().Name}");
        }

        public static object SubtractValues(object left, object right)
        {
            if (left is int l1 && right is int r1) return l1 - r1;
            if (left is long l2 && right is long r2) return l2 - r2;
            if (left is float l3 && right is float r3) return l3 - r3;
            if (left is double l4 && right is double r4) return l4 - r4;
            if (left is DateTime l5 && right is TimeSpan r5) return l5 - r5;
            if (left is DateTime l6 && right is DateTime r6) return l6 - r6;
            throw new NotSupportedException($"Cannot subtract {right?.GetType().Name} from {left?.GetType().Name}");
        }

        public static object MultiplyValues(object left, object right)
        {
            if (left is int l1 && right is int r1) return l1 * r1;
            if (left is long l2 && right is long r2) return l2 * r2;
            if (left is float l3 && right is float r3) return l3 * r3;
            if (left is double l4 && right is double r4) return l4 * r4;
            throw new NotSupportedException($"Cannot multiply {left?.GetType().Name} and {right?.GetType().Name}");
        }

        public static object DivideValues(object left, object right)
        {
            if (left is int l1 && right is int r1) { if (r1 == 0) throw new DivideByZeroException("Division by zero in expression."); return l1 / r1; }
            if (left is long l2 && right is long r2) { if (r2 == 0) throw new DivideByZeroException("Division by zero in expression."); return l2 / r2; }
            if (left is float l3 && right is float r3) { if (r3 == 0f) throw new DivideByZeroException("Division by zero in expression."); return l3 / r3; }
            if (left is double l4 && right is double r4) { if (r4 == 0d) throw new DivideByZeroException("Division by zero in expression."); return l4 / r4; }
            throw new NotSupportedException($"Cannot divide {left?.GetType().Name} by {right?.GetType().Name}");
        }

        public static object ModuloValues(object left, object right)
        {
            if (left is int l1 && right is int r1) { if (r1 == 0) throw new DivideByZeroException("Modulo by zero in expression."); return l1 % r1; }
            if (left is long l2 && right is long r2) { if (r2 == 0) throw new DivideByZeroException("Modulo by zero in expression."); return l2 % r2; }
            if (left is float l3 && right is float r3) { if (r3 == 0f) throw new DivideByZeroException("Modulo by zero in expression."); return l3 % r3; }
            if (left is double l4 && right is double r4) { if (r4 == 0d) throw new DivideByZeroException("Modulo by zero in expression."); return l4 % r4; }
            throw new NotSupportedException($"Cannot get modulo of {left?.GetType().Name} and {right?.GetType().Name}");
        }

        #endregion
    }
}
