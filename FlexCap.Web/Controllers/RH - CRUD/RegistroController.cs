
using BCrypt.Net;
using FlexCap.Web.Data;
using FlexCap.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.IO;
using Microsoft.Extensions.Logging; 

namespace FlexCap.Web.Controllers
{
    [Authorize(Roles = "HR Manager, HR Analyst, HR Consultant")]
    public class RegistroController : Controller
    {
        private readonly AppDbContext _context;

        public RegistroController(AppDbContext context /*, ILogger<RegistroController> logger */)
        {
            _context = context;
        }


        private Colaborador CriarNovoColaborador(ColaboradorViewModel model)
        {
            string finalPosition = model.Position!;
            if (model.IsManager)
            {
                finalPosition = "Project Manager";
            }

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Senha!);

            string photoUrl = model.PhotoUrl ?? "https://randomuser.me/api/portraits/lego/1.jpg";

            return new Colaborador
            {
                FullName = model.FullName!,
                Email = model.Email!,
                PasswordHash = hashedPassword,
                Position = finalPosition,
                Department = model.Department,
                TeamName = model.TeamName,
                Country = model.Country,
                PhotoUrl = photoUrl,
                Status = "Active",
                
            };
        }

        private void PopulateViewBags()
        {
            ViewBag.Departments = new SelectList(new[] { "Benefits", "Mobility", "Corporate Payments", "RH" });
            ViewBag.Teams = new SelectList(new[] { "Titans", "Code Warriors", "Bug Busters", "Pixel Pioneers", "Cloud Crusaders", "Dev Dynamos", "RH Operations" });
            ViewBag.Countries = new SelectList(new[] { "Brazil", "United States", "Italy", "Japan", "Canada" });
            ViewBag.Positions = new SelectList(new[] { "Dev Sênior", "Dev Pleno", "QA Sênior", "Project Manager", "HR Analyst", "Intern", "Designer UX/UI", "Sales Analyst", "Administrative Assistant", "QA Tester", "Marketing Manager", "HR Consultant", "Dev Back-end", "UX Strategist", "Cloud Engineer", "Full Stack Dev" });

            ViewBag.InactivityReasons = new SelectList(new[] {
                "Medical Leave",
                "Personal License",
                "Vacation",
                "Day Off",
                "Maternity Leave",
                "Suspension",
                "Other"
            });

            ViewBag.StatusOptions = new SelectList(new[] {
            new SelectListItem { Value = "Active", Text = "Active" },
            new SelectListItem { Value = "Inactive", Text = "Inactive" }
    });
        }


        [HttpGet]
        public IActionResult NovoColaborador()
        {
            ViewData["Title"] = "Cadastrar Novo Colaborador";
            ViewData["Profile"] = "Rh";
            PopulateViewBags();
            return View("CadastrarUsuario", new ColaboradorViewModel() { Status = "Active" });
        }



        // POST: Salvar Novo Colaborador 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NovoColaborador(ColaboradorViewModel model)
        {
            if (model.Email != null && await _context.Colaboradores.AnyAsync(c => c.Email == model.Email))
            {
                ModelState.AddModelError("Email", "This email is already registered.");
            }

            if (model.Senha == null)
            {
                ModelState.AddModelError("Senha", "A senha é obrigatória.");
            }

            if (!ModelState.IsValid)
            {
                PopulateViewBags();
                return View("CadastrarUsuario", model);
            }

            string senhaOriginal = model.Senha!;
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(senhaOriginal);
            string finalPosition = model.IsManager ? "Project Manager" : model.Position!;

            string photoUrl = "https://randomuser.me/api/portraits/lego/1.jpg";

            if (model.PhotoFile != null && model.PhotoFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                Directory.CreateDirectory(uploadsFolder);
                string fileName = Guid.NewGuid() + Path.GetExtension(model.PhotoFile.FileName);
                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.PhotoFile.CopyToAsync(stream);
                }
                photoUrl = "/uploads/" + fileName;
            }
            else if (!string.IsNullOrEmpty(model.PhotoUrl))
            {
                photoUrl = model.PhotoUrl;
            }

            var novoColaborador = new Colaborador
            {
                FullName = model.FullName!,
                Email = model.Email!,
                PasswordHash = hashedPassword,
                Position = finalPosition,
                Department = model.Department,
                TeamName = model.TeamName,
                Country = model.Country,
                PhotoUrl = photoUrl, 
                Status = model.Status ?? "Active",
                InactivityReason = null,
                EndDate = null
            };

            _context.Colaboradores.Add(novoColaborador);
            await _context.SaveChangesAsync(); 

            TempData["CadastroSucesso"] = true;
            TempData["NovoColaboradorNome"] = model.FullName;
            TempData["NovoColaboradorEmail"] = model.Email;
            TempData["NovoColaboradorSenha"] = senhaOriginal;

            return RedirectToAction("Listar", "Colaboradores");
        }



        [HttpGet]
        [Authorize(Roles = "HR Manager, HR Analyst, HR Consultant")]
        public async Task<IActionResult> Editar(int id)
        {
            var colaborador = await _context.Colaboradores.FindAsync(id);

            if (colaborador == null)
            {
                return NotFound();
            }

            var model = new ColaboradorViewModel
            {
                Id = colaborador.Id,
                FullName = colaborador.FullName,
                Email = colaborador.Email,
                Position = colaborador.Position,
                Department = colaborador.Department,
                TeamName = colaborador.TeamName,
                Country = colaborador.Country,
                Status = colaborador.Status,
                PhotoUrl = colaborador.PhotoUrl,
                InactivityReason = colaborador.InactivityReason,
                
                EndDate = colaborador.EndDate
            };

            PopulateViewBags();
            ViewData["Title"] = "Edit Employee: " + colaborador.FullName;

            return View(model);
        }



        [HttpPost]
        [Authorize(Roles = "HR Manager, HR Analyst, HR Consultant")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarEdicao(ColaboradorViewModel model)
        {
            if (model.Email != null && await _context.Colaboradores.AnyAsync(c => c.Email == model.Email && c.Id != model.Id))
            {
                ModelState.AddModelError("Email", "This email is already registered to another employee.");
            }

            if (!ModelState.IsValid)
            {
                PopulateViewBags();
                return View("Editar", model);
            }

            var colaboradorAntigo = await _context.Colaboradores.AsNoTracking().FirstOrDefaultAsync(c => c.Id == model.Id);

            if (colaboradorAntigo == null)
            {
                return NotFound();
            }

           
            string novoPhotoUrl = colaboradorAntigo.PhotoUrl;

            if (model.PhotoFile != null && model.PhotoFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                Directory.CreateDirectory(uploadsFolder);
                string fileName = Guid.NewGuid() + Path.GetExtension(model.PhotoFile.FileName);
                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.PhotoFile.CopyToAsync(stream);
                }
                novoPhotoUrl = "/uploads/" + fileName;
            }
            else if (string.IsNullOrEmpty(novoPhotoUrl))
            {
                novoPhotoUrl = "https://randomuser.me/api/portraits/lego/1.jpg";
            }

            string hashDaSenhaParaSalvar = string.IsNullOrEmpty(model.Senha)
                ? colaboradorAntigo.PasswordHash
                : BCrypt.Net.BCrypt.HashPassword(model.Senha);

        

            var colaboradorParaSalvar = new Colaborador
            {
                Id = model.Id, 

                PhotoUrl = novoPhotoUrl,
                PasswordHash = hashDaSenhaParaSalvar, 

                FullName = model.FullName!,
                Email = model.Email!,
                Position = model.Position!,
                Department = model.Department!,
                TeamName = model.TeamName!,
                Country = model.Country!,
                Status = model.Status!,

                InactivityReason = model.InactivityReason,
                EndDate = model.EndDate,

                ResetPasswordToken = colaboradorAntigo.ResetPasswordToken,
                ResetPasswordTokenExpiry = colaboradorAntigo.ResetPasswordTokenExpiry
            };

            _context.Colaboradores.Update(colaboradorParaSalvar);
            await _context.SaveChangesAsync();

            TempData["EdicaoSucesso"] = $"Employee {colaboradorParaSalvar.FullName} updated successfully!";

            return RedirectToAction("Listar", "Colaboradores");
        }




        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> CheckEmailAvailability(string email)
        {
            bool emailExists = await _context.Colaboradores.AnyAsync(c => c.Email == email);

            if (emailExists)
            {
                return Json(false);
            }

            return Json(true);
        }


        [HttpGet]
        public async Task<IActionResult> GetColaboradorDetails(int id)
        {
            var colaborador = await _context.Colaboradores
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (colaborador == null)
            {
                return Content("<p class='text-danger'>Colaborador não encontrado.</p>");
            }

            if (colaborador.Status?.Trim().Equals("Ativo", StringComparison.OrdinalIgnoreCase) == true)
                colaborador.Status = "Active";

            else if (colaborador.Status?.Trim().Equals("Inativo", StringComparison.OrdinalIgnoreCase) == true)
                colaborador.Status = "Inactive";

            return PartialView("ColaboradorDetailsPartial", colaborador);
        }







        // Métodos TabelaTeste (Mantidos)
        [Authorize(Roles = "HR Manager, HR Analyst, HR Consultant")]
        [HttpGet]
        public async Task<IActionResult> ListarDados()
        {
            ViewData["Title"] = "Listagem de Dados de Teste";
            ViewData["Profile"] = "Rh";
            var dados = await _context.DadosDeTeste.ToListAsync();
            return View(dados);
        }



        [Authorize(Roles = "HR Manager, HR Analyst, HR Consultant")]
        [HttpGet]
        public IActionResult CriarDado()
        {
            ViewData["Title"] = "Criar Novo Dado de Teste";
            ViewData["Profile"] = "Rh";
            return View();
        }

        [Authorize(Roles = "HR Manager, HR Analyst, HR Consultant")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarDado([Bind("NomeDoDado,Quantidade")] TabelaTeste dado)
        {
            if (ModelState.IsValid)
            {
                dado.DataCriacao = DateTime.Now;
                _context.DadosDeTeste.Add(dado);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(ListarDados));
            }
            return View(dado);
        }



        // Excluir
        [HttpPost, ActionName("Excluir")]
        [Authorize(Roles = "HR Manager, HR Analyst, HR Consultant")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirConfirmado(int id)
        {
            var colaborador = await _context.Colaboradores.FindAsync(id);

            if (colaborador != null)
            {
                _context.Colaboradores.Remove(colaborador);
                await _context.SaveChangesAsync();

                TempData["ExclusaoSucesso"] = $"Employee {colaborador.FullName} deleted successfully.";
            }

            return RedirectToAction("Listar", "Colaboradores");
        }
    }
}