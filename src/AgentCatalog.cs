public sealed record AgentDefinition(
    string Id,
    string File,
    string Name,
    string Goal,
    string[] ContextFiles);

public static class AgentCatalog
{
    public static IReadOnlyList<AgentDefinition> All { get; } =
    [
        new("research", "01-research.md", "Fikir araştırmacısı", "Hedef kullanıcıları, doğrulanması gereken alternatifleri, farklılaştırıcıları, varsayımları ve düşük maliyetli doğrulama sorularını belirle. Güncel pazar araştırması yaptığını iddia etme; doğrulanmamış noktaları soru olarak işaretle.", []),
        new("analyst", "02-analysis.md", "Ürün analisti", "Problem tanımını, hedef kullanıcıyı, küçük MVP kapsamını, kullanıcı hikâyelerini ve ölçülebilir kabul kriterlerini yaz.", ["01-research.md"]),
        new("architect", "03-architecture.md", "Yazılım mimarı", "Bakımı kolay bir mimari, bileşenleri, veri akışını, teknik kararları, riskleri ve aşamalı planı öner. Varsayımları ayır.", ["02-analysis.md"]),
        new("implementer", "04-implementation-plan.md", "Uygulama planlayıcısı", "MVP'yi sıralı ve küçük kodlama görevlerine böl. Her görev için dosyaları, beklenen davranışı ve tamamlanma ölçütlerini belirt. Kod yazdığını iddia etme.", ["02-analysis.md", "03-architecture.md"]),
        new("reviewer", "05-review.md", "Bağımsız incelemeci", "Brief ve planı bağımsız incele. Belirsizlik, güvenlik/gizlilik, olası hata, kapsam kayması ve eksik kabul kriterlerini önem derecesiyle raporla. Yalnızca gerçekten mevcut boşlukları bildir; gereksinim uydurma veya var olmayan metinden alıntı yapma.", ["02-analysis.md", "03-architecture.md", "04-implementation-plan.md"]),
        new("qa", "06-qa.md", "QA mühendisi", "Kritik senaryoları, sınır durumlarını, erişilebilirliği, hata davranışını ve elle uygulanabilir kabul kontrol listesini oluştur.", ["02-analysis.md", "04-implementation-plan.md"]),
        new("devops", "07-devops.md", "DevOps mühendisi", "Ücretsiz/yerel geliştirme, tekrarlanabilir kurulum, sırların korunması, CI, paketleme ve dağıtım önerileri ver. Ücretli bağımlılık ekleme.", ["03-architecture.md", "04-implementation-plan.md"]),
        new("marketing", "08-marketing.md", "Pazarlama yazarı", "Gerçekçi tek cümlelik değer önerisi, README girişi, demo akışı ve lansman taslağı yaz. Kullanıcı, metrik veya yayımlanmış özellik uydurma.", ["01-research.md", "02-analysis.md"])
    ];
}
