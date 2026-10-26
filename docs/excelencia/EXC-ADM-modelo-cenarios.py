#!/usr/bin/env python3
"""
Modelo de cenarios financeiros da TTSoft (ConstruTTor) - Programa de Excelencia, area ADM.

Uso:   python docs/excelencia/EXC-ADM-modelo-cenarios.py
Saida: tabelas em Markdown (as mesmas de EXC-A10-ADMINISTRATIVO-FINANCEIRO.md).

REGRAS DESTE MODELO (CRITERIOS-DE-EXCELENCIA secao 1, item 6: nao se inventam numeros)
- DECLARADO: valores ditos pelo responsavel em 2026-10-22, sem fatura. Unico dado "real".
- TABELA_PUBLICA: precos de lista publicados pelo fornecedor, consultados em 2026-10-26.
  Nao sao a fatura nem o contrato da TTSoft. Substituir pelo contrato real.
- PREMISSA: valor de cenario escolhido para ilustrar. NAO e custo, faturamento nem margem reais.
  Cada premissa diz como substitui-la por dado real (lista em EXC-A10, secao 7).
- Custos nao conhecidos (dominio, certificado, contador, ferramentas, o proprio tempo) entram
  como 0 e estao listados em FIXOS_NAO_LEVANTADOS: toda margem calculada aqui e um TETO.

Somente biblioteca padrao. Para testar outra hipotese, altere os dicionarios abaixo.
"""

# ---------------------------------------------------------------- DECLARADO (2026-10-22)
CUSTO_MENSAL_DECLARADO = 183.90      # Azure (2 bancos) + Focus (plano ate 3 CNPJs); App Service nao confirmado
RECEITA_VILA_VERDE = 89.90           # ConstruTTor, condicao especial
RECEITA_OUTFITT = 30.00              # outro produto, mesma infraestrutura, condicao especial
PRECO_ANTERIOR_AGOSTO = 99.90        # DOC-34 (agosto/2026), mais R$ 2.000 de implantacao

# ------------------------------------------------- TABELA_PUBLICA - Focus NFe (2026-10-26)
# https://focusnfe.com.br/precos/ - docs = NF-e, NFS-e, NFC-e etc. somados, salvo nos planos Retail
FOCUS_START = dict(mensal=119.90, cnpjs_incl=3, cnpj_extra=39.90, docs_por_cnpj=100, doc_extra=0.11)
FOCUS_GROWTH = dict(mensal=572.00, docs_pacote=4000, doc_extra=0.125)            # CNPJs ilimitados
FOCUS_RETAIL = dict(mensal=62.90, nfce_incl=500, nfe_incl=100, nfce_extra=0.055, nfe_extra=0.16)  # por CNPJ
FOCUS_RETAIL_PLUS = dict(mensal=657.90, nfce_pacote=9000, nfe_pacote=1000, nfce_extra=0.065, nfe_extra=0.16)
# Solo (R$ 94,90, 1 CNPJ) nao se aplica a N >= 10. Enterprise: "consulte" - fora do modelo.

# ------------------------------------------------- TABELA_PUBLICA - Asaas (2026-10-26)
ASAAS_BOLETO_PAGO = 1.99             # por boleto recebido (R$ 0,99 nos 3 primeiros meses da conta)

# ------------------------------------------------------------------------- PREMISSAS
CLIENTES = [10, 50, 100, 300]        # pedido do responsavel
PRECOS = [89.90, 99.90]              # os dois precos ja praticados/declarados (nenhum validado - EXC-VEN-001)
ALIQUOTAS = {                        # Simples Nacional, 1a faixa de cada anexo (LC 123/2006) - CONFIRMAR COM CONTADOR
    "Anexo III 6,0%": 0.060,
    "Anexo V 15,5%": 0.155,
}
NFCE_POR_CLIENTE = {"baixo": 100, "medio": 500, "alto": 1500}   # PREMISSA: alinhado aos limites dos planos Focus
NFE_POR_CLIENTE = 10                 # PREMISSA uniforme; substituir pela contagem real
CNPJ_POR_CLIENTE = 1                 # PREMISSA; lojas com filiais de CNPJ proprio elevam
RECEBIMENTOS_POR_CLIENTE_MES = 1     # PREMISSA: como a TTSoft cobra a mensalidade e LACUNA; 1 boleto/mes
INFRA_BASE = CUSTO_MENSAL_DECLARADO - FOCUS_START["mensal"]     # HIPOTESE: 64,00 = Azure se o plano da Focus for o Start
# PREMISSA ILUSTRATIVA (chute explicito): multiplo do custo de infra atual por faixa de clientes.
# Nao ha medicao de uso (DTU, GB, requisicoes). Substituir apos medir; o orcamento de infra
# suportavel por cliente e calculado separadamente e nao depende deste chute.
INFRA_FATOR = [(10, 1), (50, 2), (100, 3), (300, 5), (10**9, 8)]
FIXOS_NAO_LEVANTADOS = [
    "dominio(s)", "certificado de assinatura de codigo do instalador (se houver)",
    "contador / abertura e manutencao da empresa", "ferramentas de desenvolvimento e IA",
    "GitHub (se plano pago)", "Application Insights alem da franquia gratuita",
    "backup / retencao adicional", "equipamentos e internet", "o proprio tempo do responsavel",
]


def infra_fator(n):
    for ate, f in INFRA_FATOR:
        if n <= ate:
            return f


def focus_planos(n, nfce, nfe=NFE_POR_CLIENTE, cnpj_por_cliente=CNPJ_POR_CLIENTE):
    """Custo mensal total na Focus por plano; devolve dict nome -> R$."""
    c = n * cnpj_por_cliente
    docs = n * (nfce + nfe)
    s = FOCUS_START
    start = s["mensal"] + s["cnpj_extra"] * max(0, c - s["cnpjs_incl"]) \
        + s["doc_extra"] * max(0, docs - s["docs_por_cnpj"] * c)
    g = FOCUS_GROWTH
    growth = g["mensal"] + g["doc_extra"] * max(0, docs - g["docs_pacote"])
    r = FOCUS_RETAIL
    retail = c * r["mensal"] + c * (r["nfce_extra"] * max(0, nfce - r["nfce_incl"])
                                    + r["nfe_extra"] * max(0, nfe - r["nfe_incl"]))
    p = FOCUS_RETAIL_PLUS
    rplus = p["mensal"] + p["nfce_extra"] * max(0, n * nfce - p["nfce_pacote"]) \
        + p["nfe_extra"] * max(0, n * nfe - p["nfe_pacote"])
    return {"Start": start, "Growth": growth, "Retail": retail, "Retail+": rplus}


def cenario(n, nfce, preco, aliq):
    planos = focus_planos(n, nfce)
    plano, focus = min(planos.items(), key=lambda kv: kv[1])
    receita = n * preco
    imposto = receita * aliq
    cobranca = n * RECEBIMENTOS_POR_CLIENTE_MES * ASAAS_BOLETO_PAGO
    infra = INFRA_BASE * infra_fator(n)
    margem = receita - imposto - cobranca - focus - infra
    return dict(n=n, receita=receita, imposto=imposto, cobranca=cobranca, focus=focus, plano=plano,
                infra=infra, margem=margem, margem_cliente=margem / n, margem_pct=margem / receita)


def preco_equilibrio(n, nfce, aliq):
    """Preco mensal por cliente que zera a margem (so os custos modelados)."""
    c = cenario(n, nfce, 100.0, aliq)
    custo_sem_imposto = c["cobranca"] + c["focus"] + c["infra"]
    return custo_sem_imposto / n / (1 - aliq)


def infra_suportavel_por_cliente(n, nfce, preco, aliq):
    """Quanto de infra (Azure) por cliente o preco comporta antes de a margem zerar."""
    c = cenario(n, nfce, preco, aliq)
    return (c["receita"] - c["imposto"] - c["cobranca"] - c["focus"]) / n


def brl(x):
    s = f"{abs(x):,.2f}".replace(",", "X").replace(".", ",").replace("X", ".")
    return f"-R$ {s}" if x < 0 else f"R$ {s}"


def main():
    print("## Situacao atual declarada (fato, sem fatura)\n")
    receita = RECEITA_VILA_VERDE + RECEITA_OUTFITT
    print(f"- Receita: {brl(receita)}  |  Custo: {brl(CUSTO_MENSAL_DECLARADO)}  |  "
          f"Resultado: {brl(receita - CUSTO_MENSAL_DECLARADO)}  (antes de impostos e do tempo do responsavel)")
    print(f"- Hipotese: se a Focus for o plano Start ({brl(FOCUS_START['mensal'])}), sobram "
          f"{brl(INFRA_BASE)} para Azure e demais itens\n")

    for aliq_nome, aliq in ALIQUOTAS.items():
        for preco in PRECOS:
            print(f"\n## Preco {brl(preco)} | Simples {aliq_nome}\n")
            print("| Clientes | Vol. NFC-e/cliente | Receita | Impostos | Cobranca | Focus (plano) | Infra* | "
                  "Margem total | Margem/cliente | Margem % |")
            print("|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|")
            for n in CLIENTES:
                for vol, nfce in NFCE_POR_CLIENTE.items():
                    c = cenario(n, nfce, preco, aliq)
                    print(f"| {n} | {vol} ({nfce}) | {brl(c['receita'])} | {brl(c['imposto'])} | "
                          f"{brl(c['cobranca'])} | {brl(c['focus'])} ({c['plano']}) | {brl(c['infra'])} | "
                          f"{brl(c['margem'])} | {brl(c['margem_cliente'])} | {c['margem_pct']*100:.0f}% |")
    print("\n*Infra = premissa ilustrativa (INFRA_BASE x fator por faixa); ver docstring.\n")

    print("\n## Preco de equilibrio por cliente (margem zero; so custos modelados)\n")
    print("| Clientes | Vol. NFC-e | " + " | ".join(f"Simples {a}" for a in ALIQUOTAS) + " |")
    print("|---:|---|" + "---:|" * len(ALIQUOTAS))
    for n in CLIENTES:
        for vol, nfce in NFCE_POR_CLIENTE.items():
            print(f"| {n} | {vol} ({nfce}) | " + " | ".join(
                brl(preco_equilibrio(n, nfce, a)) for a in ALIQUOTAS.values()) + " |")

    print("\n## Infra (Azure) por cliente que o preco comporta (Simples 6,0%, preco R$ 89,90)\n")
    print("| Clientes | Vol. NFC-e | Orcamento de infra por cliente | Orcamento total de infra |")
    print("|---:|---|---:|---:|")
    for n in CLIENTES:
        for vol, nfce in NFCE_POR_CLIENTE.items():
            b = infra_suportavel_por_cliente(n, nfce, 89.90, 0.06)
            print(f"| {n} | {vol} ({nfce}) | {brl(b)} | {brl(b*n)} |")

    print("\n## Plano Focus escolhido pelo menor custo (R$/mes total)\n")
    print("| Clientes | Vol. NFC-e | " + " | ".join(["Start", "Growth", "Retail", "Retail+"]) + " |")
    print("|---:|---|---:|---:|---:|---:|")
    for n in CLIENTES:
        for vol, nfce in NFCE_POR_CLIENTE.items():
            p = focus_planos(n, nfce)
            print(f"| {n} | {vol} ({nfce}) | " + " | ".join(brl(p[k]) for k in ["Start", "Growth", "Retail", "Retail+"]) + " |")

    print("\n## Receita anual bruta (comparar com tetos do regime - confirmar com contador)\n")
    for n in CLIENTES:
        print(f"- {n} clientes x {brl(89.90)} x 12 = {brl(n*89.90*12)}  |  x {brl(99.90)} = {brl(n*99.90*12)}")

    print("\n## Custos nao levantados (tornam toda margem acima um teto)\n")
    for item in FIXOS_NAO_LEVANTADOS:
        print(f"- {item}")


if __name__ == "__main__":
    main()
