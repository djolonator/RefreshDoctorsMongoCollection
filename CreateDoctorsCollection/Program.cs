using CreateDoctorsCollection.Repository;
using CreateDoctorsCollection.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IDoctorsRepository, MongoDoctorsRepository>();
builder.Services.AddSingleton<IRefreshDoctorsLBOMongoCollectionUseCase, RefreshDoctorsLBOMongoCollectionUseCase>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/Refresh", async (HttpContext context, IRefreshDoctorsLBOMongoCollectionUseCase refreshDoctorsLBOMongoCollectionUseCase) =>
{
   
    var result = await refreshDoctorsLBOMongoCollectionUseCase.Execute();

    return TypedResults.Ok(result);
})

.WithName("Refresh")
.WithOpenApi();

app.Run();

