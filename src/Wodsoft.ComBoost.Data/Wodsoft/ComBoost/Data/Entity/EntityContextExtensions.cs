﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Wodsoft.ComBoost.Data.Entity.Metadata;

namespace Wodsoft.ComBoost.Data.Entity
{
    public static class EntityContextExtensions
    {
        public static Task<T> GetAsync<T>(this IEntityContext<T> context, object key)
            where T : class, IEntity, new()
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            var parameter = Expression.Parameter(context.Metadata.Type);
            var property = Expression.Property(parameter, context.Metadata.KeyProperty.ClrName);
            var expression = Expression.Equal(property, Expression.Constant(key));
            var lambda = Expression.Lambda<Func<T, bool>>(expression, parameter);
            return context.SingleOrDefaultAsync(context.Query(), lambda);
        }

        public static IOrderedQueryable<T> Order<T>(this IEntityContext<T> context)
            where T : class, IEntity, new()
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            return Order(context, context.Query());
        }

        public static IOrderedQueryable<T> Order<T>(this IEntityContext<T> context, IQueryable<T> query)
            where T : class, IEntity, new()
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            var parameter = Expression.Parameter(typeof(T));
            IPropertyMetadata sortProperty = context.Metadata.SortProperty ?? context.Metadata.KeyProperty;
            dynamic express = Expression.Lambda(typeof(Func<,>).MakeGenericType(typeof(T), sortProperty.ClrType), Expression.Property(parameter, sortProperty.ClrName), parameter);
            if (context.Metadata.SortDescending)
                return Queryable.OrderByDescending(query, express);
            else
                return Queryable.OrderBy(query, express);
        }

        public static IQueryable<T> InParent<T>(this IEntityContext<T> context, IQueryable<T> query, string path, object value)
            where T : class, IEntity, new()
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            var parameter = Expression.Parameter(typeof(T));
            MemberExpression member = null;
            string[] properties = path.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            Type type = context.Metadata.Type;
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = type.GetProperty(properties[i]);
                if (property == null)
                    throw new ArgumentException("父级路径错误。");
                if (member == null)
                    member = Expression.Property(parameter, property);
                else
                    member = Expression.Property(member, property);
                type = property.PropertyType;
            }
            Expression equal;
            if (type.GetTypeInfo().IsValueType)
                equal = Expression.Equal(member, Expression.Constant(value));
            else
            {
                var propertyMetadata = EntityDescriptor.GetMetadata(type);
                equal = Expression.Equal(Expression.Property(member, type.GetProperty(propertyMetadata.KeyProperty.ClrName)), Expression.Constant(value));
            }
            var express = Expression.Lambda<Func<T, bool>>(equal, parameter);
            return query.Where(express);
        }

        public static IQueryable<T> InParent<T>(this IEntityContext<T> context, IQueryable<T> query, object[] parentIds)
            where T : class, IEntity, new()
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            if (parentIds == null || parentIds.Length == 0)
                throw new ArgumentNullException(nameof(parentIds));
            if (context.Metadata.ParentProperty == null)
                throw new NotSupportedException("该实体不支持父级元素。");
            var parameter = Expression.Parameter(typeof(T));
            Expression equal = null;
            foreach (object parent in parentIds)
            {
                var item = Expression.Equal(Expression.Property(Expression.Property(parameter, context.Metadata.ParentProperty.ClrName), typeof(T).GetProperty("Index")), Expression.Constant(parent));
                if (equal == null)
                    equal = item;
                else
                    equal = Expression.Or(equal, item);
            }
            var express = Expression.Lambda<Func<T, bool>>(equal, parameter);
            return query.Where(express);

        }
    }
}