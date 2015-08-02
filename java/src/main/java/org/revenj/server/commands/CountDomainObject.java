package org.revenj.server.commands;

import org.revenj.patterns.*;
import org.revenj.server.CommandResult;
import org.revenj.server.ServerCommand;

import java.io.IOException;
import java.lang.reflect.Type;
import java.util.List;
import java.util.Map;
import java.util.Optional;

public class CountDomainObject implements ServerCommand {

	private final DomainModel domainModel;

	public CountDomainObject(DomainModel domainModel) {
		this.domainModel = domainModel;
	}

	public static final class Argument<TFormat> {
		public String Name;
		public String SpecificationName;
		public TFormat Specification;

		public Argument(String name, String specificationName, TFormat specification) {
			this.Name = name;
			this.SpecificationName = specificationName;
			this.Specification = specification;
		}

		@SuppressWarnings("unused")
		private Argument() {
		}
	}

	@Override
	public <TInput, TOutput> CommandResult<TOutput> execute(ServiceLocator locator, Serialization<TInput> input, Serialization<TOutput> output, TInput data) {
		Argument<TInput> arg;
		try {
			Type genericType = Utility.makeGenericType(Argument.class, data.getClass());
			arg = (Argument) input.deserialize(genericType, data);
		} catch (IOException e) {
			return CommandResult.badRequest(e.getMessage());
		}
		Optional<Class<?>> manifest = domainModel.find(arg.Name);
		if (!manifest.isPresent()) {
			return CommandResult.badRequest("Unable to find specified domain object: " + arg.Name);
		}
		Optional<Specification> filter;
		if (arg.SpecificationName != null && arg.SpecificationName.length() > 0) {
			Optional<Class<?>> specType = domainModel.find(arg.Name + "$" + arg.SpecificationName);
			if (!specType.isPresent()) {
				specType = domainModel.find(arg.SpecificationName);
			}
			if (!specType.isPresent()) {
				return CommandResult.badRequest("Couldn't find specification: " + arg.SpecificationName);
			}
			try {
				Specification specification = (Specification) input.deserialize((Type) specType.get(), arg.Specification);
				filter = Optional.ofNullable(specification);
			} catch (IOException e) {
				return CommandResult.badRequest("Error deserializing specification: " + arg.SpecificationName);
			}
		} else if (arg.Specification instanceof Specification) {
			filter = Optional.ofNullable((Specification) arg.Specification);
		} else {
			filter = Optional.empty();
		}
		SearchableRepository repository;
		try {
			repository = Utility.resolveSearchRepository(locator, manifest.get());
		} catch (ReflectiveOperationException e) {
			return CommandResult.badRequest("Error resolving repository for: " + arg.Name + ". Reason: " + e.getMessage());
		}
		try {
			long found = repository.query(filter.orElse(null)).count();
			return CommandResult.success(Long.toString(found), output.serialize(found));
		} catch (IOException e) {
			return CommandResult.badRequest("Error executing query. " + e.getMessage());
		}
	}
}