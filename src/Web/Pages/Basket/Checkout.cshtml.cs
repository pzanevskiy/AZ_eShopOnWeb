using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web.Interfaces;

namespace Microsoft.eShopWeb.Web.Pages.Basket;

[Authorize]
public class CheckoutModel : PageModel
{
    private readonly IBasketService _basketService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOrderService _orderService;
    private string? _username = null;
    private readonly IBasketViewModelService _basketViewModelService;
    private readonly IAppLogger<CheckoutModel> _logger;

    public CheckoutModel(IBasketService basketService,
        IBasketViewModelService basketViewModelService,
        SignInManager<ApplicationUser> signInManager,
        IOrderService orderService,
        IAppLogger<CheckoutModel> logger)
    {
        _basketService = basketService;
        _signInManager = signInManager;
        _orderService = orderService;
        _basketViewModelService = basketViewModelService;
        _logger = logger;
    }

    public BasketViewModel BasketModel { get; set; } = new BasketViewModel();

    public async Task OnGet()
    {
        await SetBasketModelAsync();
    }

    public async Task<IActionResult> OnPost(IEnumerable<BasketItemViewModel> items)
    {
        try
        {
            await SetBasketModelAsync();

            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var updateModel = items.ToDictionary(b => b.Id.ToString(), b => b.Quantity);
            var basket = await _basketService.SetQuantities(BasketModel.Id, updateModel);
            var model = basket.Items.Select(item => new
            {
                ItemId = item.CatalogItemId,
                Quantity = item.Quantity
            });
            using var ms = new MemoryStream();
            await JsonSerializer.SerializeAsync(ms, model, new JsonSerializerOptions() { WriteIndented = true });
            ms.Seek(0, SeekOrigin.Begin);

            //var uri = "https://eshopweb-test.azurewebsites.net/api/eshop-orderTrigger?code=QzATCwp-udILnEUbaq1ejm2orjoIohUOUPe2aJzsOwSbAzFuem7q7g==";
            
            //using var httpClient = new HttpClient();
            //var content = new MultipartFormDataContent();
            //var streamContent = new StreamContent(ms);
            //streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            //content.Add(streamContent, "metadata", "file");
            ////var httpResponseMessage = await httpClient.PostAsync(uri, content);
            ////httpResponseMessage.EnsureSuccessStatusCode();

            await _orderService.CreateOrderAsync(BasketModel.Id, new Address("123 Main St.", "Kent", "OH", "United States", "44240"));
            await _basketService.DeleteBasketAsync(BasketModel.Id);
        }
        catch (EmptyBasketOnCheckoutException emptyBasketOnCheckoutException)
        {
            //Redirect to Empty Basket page
            _logger.LogWarning(emptyBasketOnCheckoutException.Message);
            return RedirectToPage("/Basket/Index");
        }

        return RedirectToPage("Success");
    }

    private async Task SetBasketModelAsync()
    {
        if (_signInManager.IsSignedIn(HttpContext.User))
        {
            BasketModel = await _basketViewModelService.GetOrCreateBasketForUser(User.Identity.Name);
        }
        else
        {
            GetOrSetBasketCookieAndUserName();
            BasketModel = await _basketViewModelService.GetOrCreateBasketForUser(_username);
        }
    }

    private void GetOrSetBasketCookieAndUserName()
    {
        if (Request.Cookies.ContainsKey(Constants.BASKET_COOKIENAME))
        {
            _username = Request.Cookies[Constants.BASKET_COOKIENAME];
        }
        if (_username != null) return;

        _username = Guid.NewGuid().ToString();
        var cookieOptions = new CookieOptions();
        cookieOptions.Expires = DateTime.Today.AddYears(10);
        Response.Cookies.Append(Constants.BASKET_COOKIENAME, _username, cookieOptions);
    }
}
