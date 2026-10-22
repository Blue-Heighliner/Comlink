namespace BlueHeighliner.Comlink;

/// <summary>
/// Builds the host's own types, the ones the engine constructs for it (its configuration, its handlers, its configuration handler and the services found by convention), through dependency injection:
/// a constructor's arguments come from the container, and so does every <see langword="required"/> property that can be set, an <see langword="init"/> one included, which is how a type takes a dependency without a constructor.
/// A required property whose type is nullable is optional: it is left unset when the container has no such service.
/// </summary>
internal static class ServiceInstantiation
{
    extension(IServiceProvider services)
    {
        /// <summary>Constructs <typeparamref name="T"/> from the container and sets its required properties.</summary>
        /// <typeparam name="T">The type to construct.</typeparam>
        /// <exception cref="InvalidOperationException">A required property's type is not a service the container has.</exception>
        public T Create<T>() where T : class => (T)services.Create(typeof(T));

        /// <summary>Constructs <paramref name="type"/> from the container and sets its required properties.</summary>
        /// <param name="type">The type to construct.</param>
        /// <exception cref="InvalidOperationException">A required property's type is not a service the container has.</exception>
        public object Create(Type type)
        {
            object instance = ActivatorUtilities.CreateInstance(services, type);
            NullabilityInfoContext nullability = new();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(candidate => candidate.IsDefined(typeof(RequiredMemberAttribute), true) && candidate.GetSetMethod(true) is not null))
            {
                object? service = nullability.Create(property).WriteState is NullabilityState.Nullable
                    ? services.GetService(property.PropertyType)
                    : services.GetService(property.PropertyType) ?? throw new InvalidOperationException($"{type.Name}.{property.Name} is a required property of type {property.PropertyType.Name}, which is not a service the engine has.");
                if (service is not null)
                {
                    property.SetValue(instance, service);
                }
            }

            return instance;
        }

        /// <summary>Returns the instance the container has registered for <paramref name="type"/>, or else constructs one the way <see cref="Create(IServiceProvider, Type)"/> does.</summary>
        /// <param name="type">The type to get.</param>
        public object GetOrCreate(Type type) => services.GetService(type) ?? services.Create(type);
    }
}
