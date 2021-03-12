using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Data;
using ExchangeSharp;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.U2F.Models
{
    public class AddFido2CredentialViewModel
    {
        public AuthenticatorAttachment? AuthenticatorAttachment { get; set; }
    }

    public class RegisterFido2ViewModel : AuthenticatorAttestationRawResponse
    {
        public string Name { get; set; }
    }

    public class Fido2Controller : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFido2 _fido2;
        private readonly ApplicationDbContextFactory _applicationDbContextFactory;

        public Fido2Controller(UserManager<ApplicationUser> userManager, IFido2 fido2,
            ApplicationDbContextFactory applicationDbContextFactory)
        {
            _userManager = userManager;
            _fido2 = fido2;
            _applicationDbContextFactory = applicationDbContextFactory;
        }

        [HttpGet("")]
        [Authorize]
        public async Task List()
        {
            await using var dbContext = _applicationDbContextFactory.CreateContext();
            var userId = _userManager.GetUserId(User);

            return View(new Fido2AuthenticationViewModel()
            {
                Credentials = await dbContext.Fido2Credentials
                    .Where(credential => credential.ApplicationUserId == userId)
                    .ToListAsync()
            });
        }

        [HttpGet("register")]
        [Authorize]
        public async Task<IActionResult> Create(AddFido2CredentialViewModel viewModel)
        {
            try
            {
                await using var dbContext = _applicationDbContextFactory.CreateContext();
                var userId = _userManager.GetUserId(User);
                var user = await _userManager.GetUserAsync(User);


                // 2. Get user existing keys by username
                var existingKeys =
                    (await dbContext.Fido2Credentials.Where(credential => credential.ApplicationUserId == userId)
                        .ToListAsync()).Select(c => c.GetDescriptor()).ToList();

                // 3. Create options
                var authenticatorSelection = new AuthenticatorSelection
                {
                    RequireResidentKey = false, UserVerification = UserVerificationRequirement.Preferred
                };

                if (viewModel.AuthenticatorAttachment.HasValue)
                    authenticatorSelection.AuthenticatorAttachment = viewModel.AuthenticatorAttachment;

                var exts = new AuthenticationExtensionsClientInputs()
                {
                    Extensions = true,
                    UserVerificationIndex = true,
                    Location = true,
                    UserVerificationMethod = true,
                    BiometricAuthenticatorPerformanceBounds = new AuthenticatorBiometricPerfBounds
                    {
                        FAR = float.MaxValue, FRR = float.MaxValue
                    }
                };

                var options = _fido2.RequestNewCredential(
                    new Fido2User() {DisplayName = user.UserName, Name = user.UserName, Id = user.Id.ToBytesUTF8()},
                    existingKeys, authenticatorSelection, AttestationConveyancePreference.None, exts);

                HttpContext.Session.SetString("fido2.attestationOptions", options.ToJson());
                return View(options);
            }

            catch (Exception e)
            {
                TempData.SetStatusMessageModel(new StatusMessageModel
                {
                    Severity = StatusMessageModel.StatusSeverity.Success,
                    Html = $"FIDO2 Credentials could not be saved: {FormatException(e)}."
                });
            }

            return RedirectToAction(nameof(List));
        }

        [HttpPost("register")]
        [Authorize]
        public async Task<IActionResult> CreateResponse([FromBody] RegisterFido2ViewModel attestationResponse)
        {
            try
            {
                await using var dbContext = _applicationDbContextFactory.CreateContext();
                // 1. get the options we sent the client
                var jsonOptions = HttpContext.Session.GetString("fido2.attestationOptions");
                var options = CredentialCreateOptions.FromJson(jsonOptions);

                // 2. Create callback so that lib can verify credential id is unique to this user
                IsCredentialIdUniqueToUserAsyncDelegate callback = async (IsCredentialIdUniqueToUserParams args) =>
                    (await dbContext.Fido2Credentials.FindAsync(args.CredentialId)) is null;

                // 2. Verify and make the credentials
                var success = await _fido2.MakeNewCredentialAsync(attestationResponse, options, callback);

                // 3. Store the credentials in db
                var newCredential = new Fido2Credential()
                {
                    PublicKey = success.Result.PublicKey,
                    UserHandle = success.Result.User.Id,
                    SignatureCounter = success.Result.Counter,
                    CredType = success.Result.CredType,
                    RegDate = DateTime.Now,
                    AaGuid = success.Result.Aaguid
                };
                newCredential.SetDescriptor(new PublicKeyCredentialDescriptor(success.Result.CredentialId));

                dbContext.Fido2Credentials.AddAsync(newCredential);
                await dbContext.SaveChangesAsync();

                TempData.SetStatusMessageModel(new StatusMessageModel
                {
                    Severity = StatusMessageModel.StatusSeverity.Success,
                    Html = $"FIDO2 Credentials were saved successfully."
                });
            }
            catch (Exception e)
            {
                TempData.SetStatusMessageModel(new StatusMessageModel
                {
                    Severity = StatusMessageModel.StatusSeverity.Success,
                    Html = $"FIDO2 Credentials could not be saved: {FormatException(e)}."
                });
            }

            return RedirectToAction(nameof(List));
        }

        private string FormatException(Exception e)
        {
            return $"{e.Message}{(e.InnerException != null ? " (" + e.InnerException.Message + ")" : "")}";
        }
    }
}
