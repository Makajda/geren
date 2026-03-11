namespace Geren.Samples.Dto;

public record SimpleDto(string Greeting);
public record GenericDto<T>(T Data);
