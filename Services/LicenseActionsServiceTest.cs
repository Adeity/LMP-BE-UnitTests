using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DP_BE_LicensePortal.Model.dto.input;
using DP_BE_LicensePortal.Model.dto.output;
using DP_BE_LicensePortal.Model.Entities;
using DP_BE_LicensePortal.Repositories.Interfaces;
using DP_BE_LicensePortal.Services;
using DP_BE_LicensePortal.Services.Interfaces;
using JetBrains.Annotations;
using Moq;
using Xunit;

namespace DP_BE_LicensePortal.UnitTests.Services;

[TestSubject(typeof(LicenseActionsService))]
public class LicenseActionsServiceTest
{
    private readonly Mock<IOrganizationAccountService> _organizationAccountServiceMock = new();
    private readonly Mock<ISerialNumberDetailService> _serialNumberDetailServiceMock = new();
    private readonly Mock<IPackageDetailRepository> _packageDetailRepositoryMock = new();
    private readonly Mock<IOrganizationPackageDetailsService> _organizationPackageDetailsServiceMock = new();
    private readonly Mock<ISerialNumberDetailRepository> _serialNumberDetailRepositoryMock = new();
    private readonly Mock<IInvoiceService> _invoiceServiceMock = new();
    private readonly Mock<ISubscriptionItemService> _subscriptionItemServiceMock = new();
    private readonly Mock<IActivationServiceCaller> _activationServiceCallerMock = new();
    private readonly Mock<IMyDbContextProvider> _dbContextProviderMock = new();
    
    private LicenseActionsService CreateService()
    {
        return new LicenseActionsService(
            null,
            _organizationAccountServiceMock.Object,
            _packageDetailRepositoryMock.Object,
            _organizationPackageDetailsServiceMock.Object,
            _invoiceServiceMock.Object,
            _subscriptionItemServiceMock.Object,
            _serialNumberDetailServiceMock.Object,
            _serialNumberDetailRepositoryMock.Object,
            _activationServiceCallerMock.Object,
            _dbContextProviderMock.Object
        );
    }


    
    [Fact]
    public async Task MoveLicense_ShouldComplete_WhenValidInputsProvided()
    {
        var dto = new MoveLicenseInputDto { SourceOrganizationAccountId = 1, TargetOrganizationAccountId = 2, SerialNumberDetailId = 3 };

        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(new OrganizationAccountOutputDto { Id = 1 });
        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(2)).ReturnsAsync(new OrganizationAccountOutputDto { Id = 2 });
        _serialNumberDetailServiceMock.Setup(x => x.GetByIdAsync(3)).ReturnsAsync(new SerialNumberDetailOutputDto() { Id = 3 });
        _serialNumberDetailRepositoryMock.Setup(x => x.OrganizationHasSerialNumberDetailAsync(1, 3)).ReturnsAsync(true);
        _invoiceServiceMock.Setup(x => x.AddAsync(It.IsAny<InvoiceInputDto>())).ReturnsAsync(new InvoiceOutputDto() { Id = 100 });
        _subscriptionItemServiceMock.Setup(x => x.AddAsync(It.IsAny<SubscriptionItemInputDto>())).ReturnsAsync(new SubscriptionItemOutputDto());
        _dbContextProviderMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _dbContextProviderMock.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

        var service = CreateService();

        await service.MoveLicense(dto);

        _invoiceServiceMock.Verify(x => x.AddAsync(It.IsAny<InvoiceInputDto>()), Times.Exactly(2));
        _subscriptionItemServiceMock.Verify(x => x.AddAsync(It.IsAny<SubscriptionItemInputDto>()), Times.Exactly(2));
        _dbContextProviderMock.Verify(x => x.BeginTransaction(), Times.Once);
        _dbContextProviderMock.Verify(x => x.CommitAsync(), Times.Once);
    }


    [Fact]
    public async Task MoveLicense_ShouldThrow_WhenSourceOrgNotFound()
    {
        var dto = new MoveLicenseInputDto { SourceOrganizationAccountId = 1 };

        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync((OrganizationAccountOutputDto?)null);
        _dbContextProviderMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _dbContextProviderMock.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.MoveLicense(dto));
        Assert.Contains("Organization Account with ID: 1 not found", ex.Message);
        _dbContextProviderMock.Verify(x => x.RollbackAsync(), Times.Once);
    }

    [Fact]
    public async Task MoveLicense_ShouldThrow_WhenTargetOrgNotFound()
    {
        var dto = new MoveLicenseInputDto { SourceOrganizationAccountId = 1, TargetOrganizationAccountId = 2 };

        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(new OrganizationAccountOutputDto { Id = 1 });
        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(2)).ReturnsAsync((OrganizationAccountOutputDto?)null);
        _dbContextProviderMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _dbContextProviderMock.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.MoveLicense(dto));
        Assert.Contains("Organization Account with ID: 2 not found", ex.Message);
        _dbContextProviderMock.Verify(x => x.RollbackAsync(), Times.Once);
    }

    [Fact]
    public async Task MoveLicense_ShouldThrow_WhenSerialNumberNotFound()
    {
        var dto = new MoveLicenseInputDto { SourceOrganizationAccountId = 1, TargetOrganizationAccountId = 2, SerialNumberDetailId = 3 };

        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(new OrganizationAccountOutputDto { Id = 1 });
        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(2)).ReturnsAsync(new OrganizationAccountOutputDto { Id = 2 });
        _serialNumberDetailServiceMock.Setup(x => x.GetByIdAsync(3)).ReturnsAsync((SerialNumberDetailOutputDto?)null);
        _dbContextProviderMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _dbContextProviderMock.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.MoveLicense(dto));
        Assert.Contains("License with ID: 3 not found", ex.Message);
        _dbContextProviderMock.Verify(x => x.RollbackAsync(), Times.Once);
    }

    [Fact]
    public async Task MoveLicense_ShouldThrow_WhenLicenseNotAssignedToSource()
    {
        var dto = new MoveLicenseInputDto { SourceOrganizationAccountId = 1, TargetOrganizationAccountId = 2, SerialNumberDetailId = 3 };

        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(new OrganizationAccountOutputDto { Id = 1 });
        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(2)).ReturnsAsync(new OrganizationAccountOutputDto { Id = 2 });
        _serialNumberDetailServiceMock.Setup(x => x.GetByIdAsync(3)).ReturnsAsync(new SerialNumberDetailOutputDto() { Id = 3 });
        _serialNumberDetailRepositoryMock.Setup(x => x.OrganizationHasSerialNumberDetailAsync(1, 3)).ReturnsAsync(false);
        _dbContextProviderMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _dbContextProviderMock.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.MoveLicense(dto));
        Assert.Contains("because it is not assigned to the source organization", ex.Message);
        _dbContextProviderMock.Verify(x => x.RollbackAsync(), Times.Once);
    }

    [Fact]
    public async Task MoveLicense_ShouldRollback_WhenUnexpectedExceptionOccurs()
    {
        var dto = new MoveLicenseInputDto { SourceOrganizationAccountId = 1, TargetOrganizationAccountId = 2, SerialNumberDetailId = 3 };

        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(new OrganizationAccountOutputDto { Id = 1 });
        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(2)).ReturnsAsync(new OrganizationAccountOutputDto { Id = 2 });
        _serialNumberDetailServiceMock.Setup(x => x.GetByIdAsync(3)).ReturnsAsync(new SerialNumberDetailOutputDto() { Id = 3 });
        _serialNumberDetailRepositoryMock.Setup(x => x.OrganizationHasSerialNumberDetailAsync(1, 3)).ReturnsAsync(true);
        _invoiceServiceMock.Setup(x => x.AddAsync(It.IsAny<InvoiceInputDto>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));
        _dbContextProviderMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _dbContextProviderMock.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.MoveLicense(dto));
        Assert.Equal("Unexpected error", ex.Message);
        _dbContextProviderMock.Verify(x => x.RollbackAsync(), Times.Once);
    }
    
    [Fact]
    public async Task GenerateLicense_ShouldReturnSerialNumber_WhenValidInputsProvided()
    {
        var dto = new GenerateLicenseInputDto
        {
            OrganizationAccountId = 1,
            PackageDetailsId = 2,
            QuantityOfLicenses = 1
        };
        int resellerOrgAccountId = 99;

        _packageDetailRepositoryMock.Setup(x => x.GetByIdAsync(2)).ReturnsAsync(new PackageDetail { ID = 2, ProductNumber = "ABC123", ProductName = "Product X" });
        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(new OrganizationAccountOutputDto { Id = 1, AccountId = "ORG-1" });
        _organizationPackageDetailsServiceMock.Setup(x => x.GetByOrganizationIdAndPackageDetailsId(resellerOrgAccountId, 2))
            .ReturnsAsync(new OrganizationPackageDetailOutputDto() { Id = 100, SerialNumbersCount = 10 });
        _activationServiceCallerMock.Setup(x => x.GetLicense("1", "ABC123")).ReturnsAsync("SN-99999");
        _invoiceServiceMock.Setup(x => x.AddAsync(It.IsAny<InvoiceInputDto>())).ReturnsAsync(new InvoiceOutputDto() { Id = 123 });
        _serialNumberDetailServiceMock.Setup(x => x.GetIdBySerialNumber("SN-99999")).ReturnsAsync(77);
        _subscriptionItemServiceMock.Setup(x => x.AddAsync(It.IsAny<SubscriptionItemInputDto>())).ReturnsAsync(new SubscriptionItemOutputDto());
        _organizationAccountServiceMock.Setup(x =>
            x.UpdateOrgPackageDetailCountAsync(resellerOrgAccountId, 100, 9)).Returns(Task.CompletedTask);
        _dbContextProviderMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _dbContextProviderMock.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

        var service = CreateService();

        var result = await service.GenerateLicense(dto, resellerOrgAccountId);

        Assert.Equal("SN-99999", result.SerialNumber);
        _dbContextProviderMock.Verify(x => x.CommitAsync(), Times.Once);
    }
    
    [Fact]
    public async Task GenerateLicense_ShouldThrow_WhenPackageDetailNotFound()
    {
        var dto = new GenerateLicenseInputDto { PackageDetailsId = 2 };
        int resellerOrgAccountId = 99;

        _packageDetailRepositoryMock.Setup(x => x.GetByIdAsync(2)).ReturnsAsync((PackageDetail?)null);
        _dbContextProviderMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _dbContextProviderMock.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GenerateLicense(dto, resellerOrgAccountId));
        Assert.Contains("Package Detail with ID: 2 not found", ex.Message);
        _dbContextProviderMock.Verify(x => x.RollbackAsync(), Times.Once);
    }
    
    [Fact]
    public async Task GenerateLicense_ShouldThrow_WhenOrganizationNotFound()
    {
        var dto = new GenerateLicenseInputDto { OrganizationAccountId = 1, PackageDetailsId = 2 };
        int resellerOrgAccountId = 99;

        _packageDetailRepositoryMock.Setup(x => x.GetByIdAsync(2)).ReturnsAsync(new PackageDetail { ID = 2 });
        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync((OrganizationAccountOutputDto?)null);
        _dbContextProviderMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _dbContextProviderMock.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GenerateLicense(dto, resellerOrgAccountId));
        Assert.Contains("Organization Account with ID: 1 not found", ex.Message);
        _dbContextProviderMock.Verify(x => x.RollbackAsync(), Times.Once);
    }

    
    [Fact]
    public async Task GenerateLicense_ShouldThrow_WhenOrgPackageDetailNotFound()
    {
        var dto = new GenerateLicenseInputDto { OrganizationAccountId = 1, PackageDetailsId = 2 };
        int resellerOrgAccountId = 99;

        _packageDetailRepositoryMock.Setup(x => x.GetByIdAsync(2)).ReturnsAsync(new PackageDetail());
        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(new OrganizationAccountOutputDto());
        _organizationPackageDetailsServiceMock.Setup(x =>
            x.GetByOrganizationIdAndPackageDetailsId(resellerOrgAccountId, 2)).ReturnsAsync((OrganizationPackageDetailOutputDto?)null);
        _dbContextProviderMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _dbContextProviderMock.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GenerateLicense(dto, resellerOrgAccountId));
        Assert.Contains("Organization Package Detail with ID: 2 not found", ex.Message);
        _dbContextProviderMock.Verify(x => x.RollbackAsync(), Times.Once);
    }

    
    [Fact]
    public async Task GenerateLicense_ShouldThrow_WhenActivationReturnsError()
    {
        var dto = new GenerateLicenseInputDto { OrganizationAccountId = 1, PackageDetailsId = 2 };
        int resellerOrgAccountId = 99;

        _packageDetailRepositoryMock.Setup(x => x.GetByIdAsync(2)).ReturnsAsync(new PackageDetail { ProductNumber = "P1", ProductName = "Product" });
        _organizationAccountServiceMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(new OrganizationAccountOutputDto { Id = 1, AccountId = "ORG-1" });
        _organizationPackageDetailsServiceMock.Setup(x =>
            x.GetByOrganizationIdAndPackageDetailsId(resellerOrgAccountId, 2)).ReturnsAsync(new OrganizationPackageDetailOutputDto { Id = 100, SerialNumbersCount = 5 });

        _activationServiceCallerMock.Setup(x => x.GetLicense("1", "P1")).ReturnsAsync("ERROR");
        _dbContextProviderMock.Setup(x => x.BeginTransaction()).Returns(Task.CompletedTask);
        _dbContextProviderMock.Setup(x => x.RollbackAsync()).Returns(Task.CompletedTask);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<Exception>(() => service.GenerateLicense(dto, resellerOrgAccountId));
        Assert.Contains("WCF service call resulted in ERROR", ex.Message);
        _dbContextProviderMock.Verify(x => x.RollbackAsync(), Times.Once);
    }

}
