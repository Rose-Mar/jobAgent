using Microsoft.AspNetCore.Mvc;
using CompanyPKDApi.Models;
using CompanyPKDApi.Services;

namespace CompanyPKDApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CompanyController : ControllerBase
    {
        private readonly CompanyService _companyService;

        public CompanyController(CompanyService companyService)
        {
            _companyService = companyService;
        }

        /// <summary>
        /// Pobierz firmy po numerze PKD z paginacją
        /// </summary>
        [HttpGet("by-pkd/{pkd}")]
        public ActionResult<List<Company>> GetByPKD(
            string pkd,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 100;
            if (pageSize > 1000) pageSize = 1000;

            var allCompanies = _companyService.GetCompaniesByPKD(pkd);

            if (!allCompanies.Any())
            {
                return NotFound($"Nie znaleziono firm z PKD: {pkd}");
            }

            var totalCount = allCompanies.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var pagedCompanies = allCompanies
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                PKD = pkd,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                Companies = pagedCompanies
            });
        }

        /// <summary>
        /// Pobierz tylko licznik firm dla danego PKD
        /// </summary>
        [HttpGet("count-by-pkd/{pkd}")]
        public ActionResult GetCountByPKD(string pkd)
        {
            var count = _companyService.GetCompaniesByPKD(pkd).Count;

            return Ok(new
            {
                PKD = pkd,
                Count = count
            });
        }

        /// <summary>
        /// Pobierz firmy po wielu numerach PKD
        /// </summary>
        [HttpPost("by-pkds")]
        public ActionResult<List<Company>> GetByPKDs(
            [FromBody] List<string> pkds,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            if (pkds == null || !pkds.Any())
            {
                return BadRequest("Podaj przynajmniej jeden numer PKD");
            }

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 100;
            if (pageSize > 1000) pageSize = 1000;

            var allCompanies = _companyService.GetCompaniesByPKDs(pkds);

            var totalCount = allCompanies.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var pagedCompanies = allCompanies
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                RequestedPKDs = pkds,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                Companies = pagedCompanies
            });
        }

        /// <summary>
        /// Pobierz statystyki PKD
        /// </summary>
        [HttpGet("pkd-statistics")]
        public ActionResult GetStatistics([FromQuery] int top = 20)
        {
            var stats = _companyService.GetPKDStatistics();

            return Ok(new
            {
                TotalUniquePKDs = stats.Count,
                Statistics = stats.Take(top)
            });
        }
    }
}