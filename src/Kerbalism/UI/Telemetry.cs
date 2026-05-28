using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP.Localization;

namespace KERBALISM
{


	public static class Telemetry
	{
		// shown in place of values that require an active comm link
		const string OfflineValue = "???";

		public static void TelemetryPanel(this Panel p, Vessel v)
		{
			// avoid corner-case when this is called in a lambda after scene changes
			v = FlightGlobals.FindVessel(v.id);

			// if vessel doesn't exist anymore, leave the panel empty
			if (v == null) return;

			// get vessel data
			VesselData vd = v.KerbalismData();

			// if not a valid vessel, leave the panel empty
			if (!vd.IsSimulated) return;

			// set metadata
			p.Title(Lib.BuildString(Lib.Ellipsis(v.vesselName, Styles.ScaleStringLength(20)), " ", Lib.Color(Local.TELEMETRY_title, Lib.Kolor.LightGrey)));//"TELEMETRY"
			p.Width(Styles.ScaleWidthFloat(355.0f));
			p.paneltype = Panel.PanelType.telemetry;

			// determine offline state (matches TimedOut.Timeout condition).
			// when offline, still show the "Connection in progress / timed-out" header,
			// but always render sections — live values are shown as ??? below.
			bool offline = !vd.Connection.linked && vd.CrewCount == 0 && !v.isEVA;
			if (offline) p.Timeout(vd);

			// get resources
			VesselResources resources = ResourceCache.Get(v);

			// get crew
			var crew = Lib.CrewList(v);

			// draw the content
			Render_crew(p, crew, offline);
			if (Features.Science) Render_science(p, v, vd, offline);
			Render_greenhouse(p, vd, offline);
			Render_supplies(p, v, vd, resources, offline);
			Render_habitat(p, v, vd, offline);
			Render_environment(p, v, vd, offline);

			// collapse eva kerbal sections into one
			if (v.isEVA) p.Collapse(Local.TELEMETRY_EVASUIT);//"EVA SUIT"
		}


		static void Render_environment(Panel p, Vessel v, VesselData vd, bool offline)
		{
			// don't show env panel in eva kerbals
			if (v.isEVA) return;

			// get all sensor readings
			HashSet<string> readings = new HashSet<string>();
			if (v.loaded)
			{
				foreach (var s in PartModuleCache.GetModules<Sensor>(v))
				{
					if (s.isEnabled)
					readings.Add(s.type);
				}
			}
			else
			{
				foreach (ProtoPartModuleSnapshot m in ProtoPartModuleCache.GetModules(v.protoVessel, "Sensor"))
				{
					readings.Add(Lib.Proto.GetString(m, "type"));
				}
			}
			readings.Remove(string.Empty);

			p.AddSection(Local.TELEMETRY_ENVIRONMENT);//"ENVIRONMENT"

			foreach (string type in readings)
			{
				if (offline)
					p.AddContent(Sensor.DisplayName(type), OfflineValue);
				else
					p.AddContent(Sensor.DisplayName(type), Sensor.Telemetry_content(v, vd, type), Sensor.Telemetry_tooltip(v, vd, type));
			}
			if (readings.Count == 0) p.AddContent("<i>"+Local.TELEMETRY_nosensorsinstalled +"</i>");//no sensors installed
		}

		static void Render_habitat(Panel p, Vessel v, VesselData vd, bool offline)
		{
			// if habitat feature is disabled, do not show the panel
			if (!Features.Habitat) return;

			// if vessel is unmanned, do not show the panel
			if (vd.CrewCount == 0) return;

			// render panel, add some content based on enabled features
			p.AddSection(Local.TELEMETRY_HABITAT);//"HABITAT"
			if (Features.Poisoning) p.AddContent(Local.TELEMETRY_co2level, offline ? OfflineValue : Lib.Color(vd.Poisoning > Settings.PoisoningThreshold, Lib.HumanReadablePerc(vd.Poisoning, "F2"), Lib.Kolor.Yellow));//"co2 level"
			if (Features.Radiation && v.isEVA) p.AddContent(Local.TELEMETRY_radiation, offline ? OfflineValue : Lib.HumanReadableRadiation(vd.EnvHabitatRadiation));//"radiation"

			if (!v.isEVA)
			{
				if (Features.Pressure) p.AddContent(Local.TELEMETRY_pressure, offline ? OfflineValue : Lib.HumanReadableNormalizedPressure(vd.Pressure));//"pressure"
				if (Features.Shielding) p.AddContent(Local.TELEMETRY_shielding, offline ? OfflineValue : Lib.HumanReadableShieldingLevel(vd.Shielding));//"shielding"
				if (Features.LivingSpace) p.AddContent(Local.TELEMETRY_livingspace, offline ? OfflineValue : Lib.HumanReadableLivingSpace(vd.LivingSpace));//"living space"
				if (Features.Comfort)
				{
					if (offline)
						p.AddContent(Local.TELEMETRY_comfort, OfflineValue);
					else
						p.AddContent(Local.TELEMETRY_comfort, vd.Comforts.Summary(), vd.Comforts.Tooltip());//"comfort"
				}
				if (Features.Pressure && Settings.LifeSupportAtmoLoss > 0)
					p.AddContent(Local.TELEMETRY_EVAStatus, offline ? OfflineValue : (vd.Evas > 1 ? Local.TELEMETRY_EVAStatus1 : Local.TELEMETRY_EVAStatus2), null); // "EVA Status" / "safe" / "risky"
			}
		}

		static void Render_science(Panel p, Vessel v, VesselData vd, bool offline)
		{
			// don't show science panel in eva kerbals
			if (v.isEVA) return;

			p.AddSection(Local.TELEMETRY_TRANSMISSION);//"TRANSMISSION"

			// live transmission state: hidden behind comm link
			if (offline)
			{
				p.AddContent(Local.TELEMETRY_maxtransmissionrate, OfflineValue);
				p.AddContent(Local.TELEMETRY_target, OfflineValue);
			}
			else if (vd.filesTransmitted.Count > 0)
			{
				double transmitRate = 0.0;
				StringBuilder tooltip = new StringBuilder();
				tooltip.Append(string.Format("<align=left /><b>{0,-15}\t{1}</b>\n", Local.TELEMETRY_TRANSMISSION_rate, Local.TELEMETRY_filetransmitted));//"rate""file transmitted"
				for (int i = 0; i < vd.filesTransmitted.Count; i++)
				{
					transmitRate += vd.filesTransmitted[i].transmitRate;
					tooltip.Append(string.Format("{0,-15}\t{1}", Lib.HumanReadableDataRate(vd.filesTransmitted[i].transmitRate), Lib.Ellipsis(vd.filesTransmitted[i].subjectData.FullTitle, 40u)));
					if (i < vd.filesTransmitted.Count - 1) tooltip.Append("\n");
				}

				p.AddContent(Local.TELEMETRY_transmitting, Lib.BuildString(vd.filesTransmitted.Count.ToString(), vd.filesTransmitted.Count > 1 ? " files at " : " file at ",  Lib.HumanReadableDataRate(transmitRate)), tooltip.ToString());//"transmitting"
				p.AddContent(Local.TELEMETRY_target, vd.Connection.target_name);//"target"
			}
			else
			{
				p.AddContent(Local.TELEMETRY_maxtransmissionrate, Lib.HumanReadableDataRate(vd.Connection.rate));//"max transmission rate"
				p.AddContent(Local.TELEMETRY_target, vd.Connection.target_name);//"target"
			}

			// total science gained by vessel (always shown, even offline)
			p.AddContent(Local.TELEMETRY_totalsciencetransmitted, Lib.HumanReadableScience(vd.scienceTransmitted, false));//"total science transmitted"

			// time since last science transmission (always shown, even offline)
			string lastXmit;
			if (vd.lastScienceTransmittedUT < 0.0)
			{
				lastXmit = Local.Generic_NEVER;
			}
			else
			{
				double dt = Planetarium.GetUniversalTime() - vd.lastScienceTransmittedUT;
				lastXmit = dt <= 0.0 ? Local.TELEMETRY_nochange : Lib.HumanReadableDuration(dt);
			}
			p.AddContent("last science transmitted", lastXmit);
		}

		static void Render_supplies(Panel p, Vessel v, VesselData vd, VesselResources resources, bool offline)
		{
			int supplies = 0;
			// for each supply
			foreach (Supply supply in Profile.supplies)
			{
				// get resource info
				ResourceInfo res = resources.GetResource(v, supply.resource);

				// only show estimate if the resource is present
				if (res.Capacity <= 1e-10) continue;

				// render panel title, if not done already
				if (supplies == 0) p.AddSection(Local.TELEMETRY_SUPPLIES);//"SUPPLIES"

				// determine label
				var resource = PartResourceLibrary.Instance.resourceDefinitions[supply.resource];
				string label = Lib.SpacesOnCaps(resource.displayName).ToLower();

				if (offline)
				{
					p.AddContent(label, OfflineValue);
					++supplies;
					continue;
				}

				StringBuilder sb = new StringBuilder();

				sb.Append("<align=left />");
				if (res.AverageRate != 0.0)
				{
					sb.Append(Lib.Color(res.AverageRate > 0.0,
						Lib.BuildString("+", Lib.HumanOrSIRate(Math.Abs(res.AverageRate), resource.id)), Lib.Kolor.PosRate,
						Lib.BuildString("-", Lib.HumanOrSIRate(Math.Abs(res.AverageRate), resource.id)), Lib.Kolor.NegRate,
						true));
				}
				else
				{
					sb.Append("<b>");
					sb.Append(Local.TELEMETRY_nochange);//no change
					sb.Append("</b>");
				}

				if (res.AverageRate < 0.0 && res.Level < 0.0001)
				{
					sb.Append(" <i>");
					sb.Append(Local.TELEMETRY_empty);//(empty)
					sb.Append("</i>");
				}
				else if (res.AverageRate > 0.0 && res.Level > 0.9999)
				{
					sb.Append(" <i>");
					sb.Append(Local.TELEMETRY_full);//(full)
					sb.Append("</i>");

				}
				else sb.Append("   "); // spaces to prevent alignement issues

				sb.Append("\t");
				sb.Append(Lib.HumanOrSIAmount(res.Amount, resource.id));
				sb.Append("/");
				sb.Append(Lib.HumanOrSIAmount(res.Capacity, resource.id));
				sb.Append(" (");
				sb.Append(res.Level.ToString("P0"));
				sb.Append(")");

				List<SupplyData.ResourceBrokerRate> brokers = vd.Supply(supply.resource).ResourceBrokers;
				if (brokers.Count > 0)
				{
					sb.Append("\n<b>------------    \t------------</b>");
					foreach (SupplyData.ResourceBrokerRate rb in brokers)
					{
						sb.Append("\n");
						sb.Append(Lib.Color(rb.rate > 0.0,
							Lib.BuildString("+", Lib.HumanOrSIRate(Math.Abs(rb.rate), resource.id), "   "), Lib.Kolor.PosRate, // spaces to mitigate alignement issues
							Lib.BuildString("-", Lib.HumanOrSIRate(Math.Abs(rb.rate), resource.id), "   "), Lib.Kolor.NegRate, // spaces to mitigate alignement issues
							true));
						sb.Append("\t");
						sb.Append(rb.broker.Title);
					}
				}

				string rate_tooltip = sb.ToString();

				// finally, render resource supply
				p.AddContent(label, Lib.HumanReadableDuration(res.DepletionTime()), rate_tooltip);
				++supplies;
			}
		}


		static void Render_crew(Panel p, List<ProtoCrewMember> crew, bool offline)
		{
			// do nothing if there isn't a crew, or if there are no rules
			if (crew.Count == 0 || Profile.rules.Count == 0) return;

			// panel section
			p.AddSection(Local.TELEMETRY_VITALS);//"VITALS"

			// for each crew
			foreach (ProtoCrewMember kerbal in crew)
			{
				// get kerbal data from DB
				KerbalData kd = DB.Kerbal(kerbal.name);

				// generate kerbal name
				string name = kerbal.name.ToLower().Replace(" kerman", string.Empty);

				if (offline)
				{
					// vitals require live telemetry; render row but hide health/stress
					p.AddContent(Lib.Ellipsis(name, Styles.ScaleStringLength(30)), OfflineValue);
					continue;
				}

				// analyze issues
				UInt32 health_severity = 0;
				UInt32 stress_severity = 0;

				// generate tooltip
				List<string> tooltips = new List<string>();
				foreach (Rule r in Profile.rules)
				{
					// get rule data
					RuleData rd = kd.Rule(r.name);

					// add to the tooltip
					tooltips.Add(Lib.BuildString("<b>", Lib.HumanReadablePerc(rd.problem / r.fatal_threshold), "</b>\t", r.title));

					// analyze issue
					if (rd.problem > r.danger_threshold)
					{
						if (!r.breakdown) health_severity = Math.Max(health_severity, 2);
						else stress_severity = Math.Max(stress_severity, 2);
					}
					else if (rd.problem > r.warning_threshold)
					{
						if (!r.breakdown) health_severity = Math.Max(health_severity, 1);
						else stress_severity = Math.Max(stress_severity, 1);
					}
				}
				string tooltip = Lib.BuildString("<align=left />", String.Join("\n", tooltips.ToArray()));

				// render selectable title
				p.AddContent(Lib.Ellipsis(name, Styles.ScaleStringLength(30)), kd.disabled ? Lib.Color(Local.TELEMETRY_HYBERNATED, Lib.Kolor.Cyan) : string.Empty);//"HYBERNATED"
				p.AddRightIcon(health_severity == 0 ? Textures.health_white : health_severity == 1 ? Textures.health_yellow : Textures.health_red, tooltip);
				p.AddRightIcon(stress_severity == 0 ? Textures.brain_white : stress_severity == 1 ? Textures.brain_yellow : Textures.brain_red, tooltip);
			}
		}

		static void Render_greenhouse(Panel p, VesselData vd, bool offline)
		{
			// do nothing without greenhouses
			if (vd.Greenhouses.Count == 0) return;

			// panel section
			p.AddSection(Local.TELEMETRY_GREENHOUSE);//"GREENHOUSE"

			// for each greenhouse
			for (int i = 0; i < vd.Greenhouses.Count; ++i)
			{
				var greenhouse = vd.Greenhouses[i];

				string label = Lib.BuildString(Local.TELEMETRY_crop, " #", (i + 1).ToString());

				if (offline)
				{
					p.AddContent(label, OfflineValue);
					continue;
				}

				// state string
				string state = greenhouse.issue.Length > 0
				  ? Lib.Color(greenhouse.issue, Lib.Kolor.Yellow)
				  : greenhouse.growth >= 0.99
				  ? Lib.Color(Local.TELEMETRY_readytoharvest, Lib.Kolor.Green)//"ready to harvest"
				  : Local.TELEMETRY_growing;//"growing"

				// tooltip with summary
				string tooltip = greenhouse.growth < 0.99 ? Lib.BuildString
				(
				  "<align=left />",
				  Local.TELEMETRY_timetoharvest, "\t<b>", Lib.HumanReadableDuration(greenhouse.tta), "</b>\n",//"time to harvest"
				  Local.TELEMETRY_growth, "\t\t<b>", Lib.HumanReadablePerc(greenhouse.growth), "</b>\n",//"growth"
				  Local.TELEMETRY_naturallighting, "\t<b>", Lib.HumanReadableFlux(greenhouse.natural), "</b>\n",//"natural lighting"
				  Local.TELEMETRY_artificiallighting, "\t<b>", Lib.HumanReadableFlux(greenhouse.artificial), "</b>"//"artificial lighting"
				) : string.Empty;

				// render it
				p.AddContent(label, state, tooltip);//"crop"

				// issues too, why not
				p.AddRightIcon(greenhouse.issue.Length == 0 ? Textures.plant_white : Textures.plant_yellow, tooltip);
			}
		}
	}


} // KERBALISM
