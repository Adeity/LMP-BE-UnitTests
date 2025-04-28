using System;
using DP_BE_LicensePortal.Controllers;
using Xunit;
using Moq;
using DP_BE_LicensePortal.Controllers;
using DP_BE_LicensePortal.Services.Interfaces;
using DP_BE_LicensePortal.Model.dto.input;
using DP_BE_LicensePortal.Model.dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using DP_BE_LicensePortal.Services;
using JetBrains.Annotations;
using Xunit;

namespace DP_BE_LicensePortal.UnitTests.Controllers;

[TestSubject(typeof(LicenseActionsController))]
public class LicenseActionsControllerTest
{
    private readonly Mock<ILicenseActionsService> _licenseActionsServiceMock = new();
    private readonly Mock<IOrganizationAccountService> _organizationAccountServiceMock = new();
    private readonly Mock<ICustomUserManager> _userManagerMock = new();

    private LicenseActionsController CreateController()
    {
        var controller = new LicenseActionsController(
            null,
            _licenseActionsServiceMock.Object,
            _organizationAccountServiceMock.Object,
            null,
            _userManagerMock.Object
        );

        controller.ControllerContext = new ControllerContext()
        {
            HttpContext = new DefaultHttpContext()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "testuser") }))
            }
        };

        return controller;
    }

    [Fact]
    public async Task GenerateLicense_ShouldReturnOk_WhenInputsAreValid()
    {
        // Arrange
        var dto = new GenerateLicenseInputDto { OrganizationAccountId = 10, PackageDetailsId = 5 };
        var resellerOrgAccountId = 1;

        _userManagerMock.Setup(x => x.GetOrgByPrincipalAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(resellerOrgAccountId);

        _organizationAccountServiceMock.Setup(x => x.IsChildOrganizationOfReseller(dto.OrganizationAccountId, resellerOrgAccountId))
            .ReturnsAsync(true);

        _licenseActionsServiceMock.Setup(x => x.GenerateLicense(dto, resellerOrgAccountId))
            .ReturnsAsync(new SerialNumberDetailOutputDto
            {
                Id = 123,
                SerialNumber = "SN-123456"
            });

        // Act
        var controller = CreateController();
        var result = await controller.GenerateLicense(dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var outputDto = Assert.IsType<SerialNumberDetailOutputDto>(okResult.Value);
        Assert.Equal("SN-123456", outputDto.SerialNumber);
        Assert.Equal(123, outputDto.Id);
    }


    [Fact]
    public async Task GenerateLicense_ShouldReturnNotFound_WhenResellerOrgIsNull()
    {
        // Arrange
        var dto = new GenerateLicenseInputDto { OrganizationAccountId = 10, PackageDetailsId = 5 };
        _userManagerMock.Setup(x => x.GetOrgByPrincipalAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((int?)null); // simulate null resellerOrg

        // Act
        var controller = CreateController();
        var result = await controller.GenerateLicense(dto);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("Reseller organization account not found for logged user.", notFound.Value);
    }

    [Fact]
    public async Task GenerateLicense_ShouldReturnUnauthorized_WhenOrganizationNotUnderReseller()
    {
        // Arrange
        var dto = new GenerateLicenseInputDto { OrganizationAccountId = 10, PackageDetailsId = 5 };
        var resellerOrgAccountId = 1;

        _userManagerMock.Setup(x => x.GetOrgByPrincipalAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(resellerOrgAccountId);

        _organizationAccountServiceMock.Setup(x => x.IsChildOrganizationOfReseller(dto.OrganizationAccountId, resellerOrgAccountId))
            .ReturnsAsync(false);

        // Act
        var controller = CreateController();
        var result = await controller.GenerateLicense(dto);

        // Assert
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var error = Assert.IsType<ErrorResponseDto>(unauthorized.Value);
        Assert.Equal("INVALID_SOURCE_ORGANIZATION", error.Code);
    }

    [Fact]
    public async Task GenerateLicense_ShouldReturnInternalServerError_OnException()
    {
        // Arrange
        var dto = new GenerateLicenseInputDto { OrganizationAccountId = 10, PackageDetailsId = 5 };
        var resellerOrgAccountId = 1;

        _userManagerMock.Setup(x => x.GetOrgByPrincipalAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(resellerOrgAccountId);

        _organizationAccountServiceMock.Setup(x => x.IsChildOrganizationOfReseller(It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("Unexpected failure"));

        // Act
        var controller = CreateController();
        var result = await controller.GenerateLicense(dto);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponseDto>(objectResult.Value);
        Assert.Equal("LICENSE_GENERATION_FAILED", error.Code);
        Assert.Equal("Unexpected failure", error.Details["error"]);
    }

    [Fact]
    public async Task MoveLicense_ShouldReturnOk_WhenInputsAreValid()
    {
        // Arrange
        var dto = new MoveLicenseInputDto
        {
            SourceOrganizationAccountId = 1,
            TargetOrganizationAccountId = 2,
            SerialNumberDetailId = 99
        };

        _userManagerMock.Setup(x => x.GetOrgByPrincipalAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(5);
        _organizationAccountServiceMock.Setup(x => x.IsChildOrganizationOfReseller(dto.SourceOrganizationAccountId, 5)).ReturnsAsync(true);
        _organizationAccountServiceMock.Setup(x => x.IsChildOrganizationOfReseller(dto.TargetOrganizationAccountId, 5)).ReturnsAsync(true);

        // Act
        var controller = CreateController();
        var result = await controller.MoveLicense(dto);

        // Assert
        Assert.IsType<OkResult>(result.Result);
    }

    [Fact]
    public async Task MoveLicense_ShouldReturnUnauthorized_WhenSourceOrgNotUnderReseller()
    {
        var dto = new MoveLicenseInputDto { SourceOrganizationAccountId = 1, TargetOrganizationAccountId = 2, SerialNumberDetailId = 1};

        _userManagerMock.Setup(x => x.GetOrgByPrincipalAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(5);
        _organizationAccountServiceMock.Setup(x => x.IsChildOrganizationOfReseller(dto.SourceOrganizationAccountId, 5)).ReturnsAsync(false);

        var controller = CreateController();
        var result = await controller.MoveLicense(dto);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var error = Assert.IsType<ErrorResponseDto>(unauthorized.Value);
        Assert.Equal("INVALID_SOURCE_ORGANIZATION", error.Code);
    }

    [Fact]
    public async Task MoveLicense_ShouldReturnUnauthorized_WhenTargetOrgNotUnderReseller()
    {
        var dto = new MoveLicenseInputDto { SourceOrganizationAccountId = 1, TargetOrganizationAccountId = 2, SerialNumberDetailId = 1};

        _userManagerMock.Setup(x => x.GetOrgByPrincipalAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(5);
        _organizationAccountServiceMock.Setup(x => x.IsChildOrganizationOfReseller(dto.SourceOrganizationAccountId, 5)).ReturnsAsync(true);
        _organizationAccountServiceMock.Setup(x => x.IsChildOrganizationOfReseller(dto.TargetOrganizationAccountId, 5)).ReturnsAsync(false);

        var controller = CreateController();
        var result = await controller.MoveLicense(dto);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var error = Assert.IsType<ErrorResponseDto>(unauthorized.Value);
        Assert.Equal("INVALID_SOURCE_ORGANIZATION", error.Code); // same code as source
    }

    [Fact]
    public async Task MoveLicense_ShouldReturnNotFound_WhenResellerOrgIdIsNull()
    {
        var dto = new MoveLicenseInputDto { SourceOrganizationAccountId = 1, TargetOrganizationAccountId = 2, SerialNumberDetailId = 1};

        _userManagerMock.Setup(x => x.GetOrgByPrincipalAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((int?)null);

        var controller = CreateController();
        var result = await controller.MoveLicense(dto);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("Reseller organization account not found for logged user.", notFound.Value);
    }

    [Fact]
    public async Task MoveLicense_ShouldReturnInternalServerError_OnUnhandledException()
    {
        var dto = new MoveLicenseInputDto { SourceOrganizationAccountId = 1, TargetOrganizationAccountId = 2, SerialNumberDetailId = 1};

        _userManagerMock.Setup(x => x.GetOrgByPrincipalAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(5);
        _organizationAccountServiceMock.Setup(x => x.IsChildOrganizationOfReseller(It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("unexpected boom"));

        var controller = CreateController();
        var result = await controller.MoveLicense(dto);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponseDto>(objectResult.Value);
        Assert.Equal("LICENSE_MOVE_FAILED", error.Code);
        Assert.Equal("unexpected boom", error.Details["error"]);
    }
}