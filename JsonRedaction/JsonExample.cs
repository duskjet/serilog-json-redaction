public class JsonExample
{
    public Guid Id { get; set; }
    public string CreditCard { get; set; }
    public JsonNestedExample Nested { get; set; }

}

public class JsonNestedExample
{
    public string SecretValue { get; set; }
}