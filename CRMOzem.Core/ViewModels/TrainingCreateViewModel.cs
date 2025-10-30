using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class TrainingCreateViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime TrainingDate { get; set; }
        public string Link { get; set; } = string.Empty;
        public bool IsPublished { get; set; }

        // Eğitim soruları
        public List<TrainingQuestionViewModel>? Questions { get; set; }

        // Kullanıcı seçimi
        public List<SelectListItem>? AvailableUsers { get; set; }

        // JS tarafından gönderilen comma-separated string
        public string? SelectedUserIdsString { get; set; }

        // Parse edilmiş liste (otomatik)
        public List<string> SelectedUserIds =>
            string.IsNullOrWhiteSpace(SelectedUserIdsString)
                ? new List<string>()
                : SelectedUserIdsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(x => x.Trim()).ToList();

    }
}
